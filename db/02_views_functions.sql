-- =====================================================================
--  Прайм Бургер — представления, функции, триггеры
--  Файл 2/3
-- =====================================================================
SET client_min_messages = WARNING;

-- ---------------------------------------------------------------------
-- Представление: позиции заказа с вычисленной суммой
-- ---------------------------------------------------------------------
CREATE OR REPLACE VIEW v_order_items AS
SELECT oi.id,
       oi.order_id,
       d.name                                            AS dish_name,
       c.name                                            AS category_name,
       oi.quantity,
       oi.unit_price,
       oi.discount_percent,
       ROUND(oi.quantity * oi.unit_price
             * (1 - oi.discount_percent / 100.0), 2)     AS line_total
FROM order_items oi
JOIN dishes d     ON d.id = oi.dish_id
JOIN categories c ON c.id = d.category_id;

-- Представление: итоговая сумма по каждому заказу
CREATE OR REPLACE VIEW v_order_totals AS
SELECT o.id AS order_id,
       o.status,
       o.table_id,
       COALESCE(SUM(vi.line_total), 0) AS order_total,
       COALESCE(SUM(vi.quantity), 0)   AS items_count
FROM orders o
LEFT JOIN v_order_items vi ON vi.order_id = o.id
GROUP BY o.id, o.status, o.table_id;

-- Представление: заказ с выведенными столом, официантом и суммой.
-- Официант определяется закреплением стола заказа за официантом в смене
-- (shifts.work_date = дата заказа). Это убирает прямую связь orders→users.
CREATE OR REPLACE VIEW v_orders AS
SELECT o.id,
       o.table_id,
       t.number                                  AS table_number,
       asg.waiter_id,
       u.last_name || ' ' || u.first_name        AS waiter_name,
       o.status,
       o.created_at,
       o.placed_at,
       vt.order_total,
       vt.items_count
FROM orders o
JOIN restaurant_tables t ON t.id = o.table_id
JOIN v_order_totals vt   ON vt.order_id = o.id
LEFT JOIN LATERAL (
    -- единственное закрепление стола за официантом на дату заказа
    SELECT s.waiter_id
    FROM shift_tables st
    JOIN shifts s ON s.id = st.shift_id
    WHERE st.table_id = o.table_id
      AND s.work_date = o.created_at::date
    LIMIT 1
) asg ON TRUE
LEFT JOIN users u ON u.id = asg.waiter_id;

-- ---------------------------------------------------------------------
-- Функция: эффективная скидка на блюдо на заданную дату
-- (максимальная среди действующих акций, охватывающих это блюдо)
-- ---------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_effective_discount(p_dish_id INTEGER, p_on_date DATE)
RETURNS NUMERIC AS $$
    SELECT COALESCE(MAX(p.discount_percent), 0)
    FROM promotions p
    JOIN promotion_dishes pd ON pd.promotion_id = p.id
    WHERE p.is_active
      AND p_on_date BETWEEN p.start_date AND p.end_date
      AND pd.dish_id = p_dish_id;
$$ LANGUAGE sql STABLE;

-- ---------------------------------------------------------------------
-- Функция: добавление блюда в заказ с контролем склада (транзакционно)
-- Возвращает текст сообщения по ТЗ.
-- ---------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_add_order_item(p_order_id INTEGER, p_dish_id INTEGER, p_qty INTEGER)
RETURNS TEXT AS $$
DECLARE
    v_status     order_status;
    v_available  INTEGER;
    v_price      NUMERIC(10,2);
    v_discount   NUMERIC(5,2);
    v_dish_name  TEXT;
    v_existing   INTEGER;
BEGIN
    IF p_qty <= 0 THEN
        RETURN 'Ошибка: количество порций должно быть больше нуля.';
    END IF;

    SELECT status INTO v_status FROM orders WHERE id = p_order_id FOR UPDATE;
    IF v_status IS NULL THEN
        RETURN 'Ошибка: заказ не найден.';
    END IF;
    IF v_status <> 'COMPOSING' THEN
        RETURN 'Ошибка: заказ уже оформлен, добавление блюд недоступно.';
    END IF;

    SELECT d.name, d.base_price INTO v_dish_name, v_price FROM dishes d WHERE d.id = p_dish_id;
    IF v_dish_name IS NULL THEN
        RETURN 'Ошибка: блюдо не найдено.';
    END IF;

    SELECT quantity INTO v_available FROM stock WHERE dish_id = p_dish_id FOR UPDATE;
    v_available := COALESCE(v_available, 0);
    IF v_available < p_qty THEN
        RETURN format('Невозможно добавить блюдо "%s" в количестве %s порций. Сейчас доступно %s порций данного блюда.',
                      v_dish_name, p_qty, v_available);
    END IF;

    v_discount := fn_effective_discount(p_dish_id, CURRENT_DATE);

    -- Списание со склада
    UPDATE stock SET quantity = quantity - p_qty WHERE dish_id = p_dish_id;
    INSERT INTO stock_movements(dish_id, change, reason)
    VALUES (p_dish_id, -p_qty, format('Добавление в заказ №%s', p_order_id));

    -- Добавление/увеличение позиции
    SELECT quantity INTO v_existing FROM order_items WHERE order_id = p_order_id AND dish_id = p_dish_id;
    IF v_existing IS NULL THEN
        INSERT INTO order_items(order_id, dish_id, quantity, unit_price, discount_percent)
        VALUES (p_order_id, p_dish_id, p_qty, v_price, v_discount);
    ELSE
        UPDATE order_items SET quantity = quantity + p_qty
        WHERE order_id = p_order_id AND dish_id = p_dish_id;
    END IF;

    RETURN format('Блюдо (%s) в количестве %s порций было успешно добавлено в Заказ №%s',
                  v_dish_name, p_qty, p_order_id);
END;
$$ LANGUAGE plpgsql;

-- ---------------------------------------------------------------------
-- Функция: удаление блюда из заказа с возвратом порций на склад
-- ---------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_remove_order_item(p_order_id INTEGER, p_dish_id INTEGER)
RETURNS TEXT AS $$
DECLARE
    v_status    order_status;
    v_qty       INTEGER;
    v_dish_name TEXT;
BEGIN
    SELECT status INTO v_status FROM orders WHERE id = p_order_id FOR UPDATE;
    IF v_status IS NULL THEN
        RETURN 'Ошибка: заказ не найден.';
    END IF;
    IF v_status <> 'COMPOSING' THEN
        RETURN 'Ошибка: заказ уже оформлен, изменение состава недоступно.';
    END IF;

    SELECT oi.quantity, d.name INTO v_qty, v_dish_name
    FROM order_items oi JOIN dishes d ON d.id = oi.dish_id
    WHERE oi.order_id = p_order_id AND oi.dish_id = p_dish_id;

    IF v_qty IS NULL THEN
        RETURN 'Ошибка: данное блюдо отсутствует в заказе.';
    END IF;

    UPDATE stock SET quantity = quantity + v_qty WHERE dish_id = p_dish_id;
    INSERT INTO stock_movements(dish_id, change, reason)
    VALUES (p_dish_id, v_qty, format('Удаление из заказа №%s', p_order_id));

    DELETE FROM order_items WHERE order_id = p_order_id AND dish_id = p_dish_id;

    RETURN format('Блюдо (%s) в количестве %s порций было успешно удалено из Заказа №%s',
                  v_dish_name, v_qty, p_order_id);
END;
$$ LANGUAGE plpgsql;

-- ---------------------------------------------------------------------
-- Функция: оформление заказа (фиксация даты/времени)
-- ---------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_place_order(p_order_id INTEGER)
RETURNS TEXT AS $$
DECLARE
    v_status order_status;
    v_cnt    INTEGER;
BEGIN
    SELECT status INTO v_status FROM orders WHERE id = p_order_id FOR UPDATE;
    IF v_status IS NULL THEN
        RETURN 'Ошибка: заказ не найден.';
    END IF;
    IF v_status <> 'COMPOSING' THEN
        RETURN 'Ошибка: заказ уже оформлен.';
    END IF;
    SELECT COUNT(*) INTO v_cnt FROM order_items WHERE order_id = p_order_id;
    IF v_cnt = 0 THEN
        RETURN 'Ошибка: нельзя оформить пустой заказ.';
    END IF;

    UPDATE orders SET status = 'PLACED', placed_at = now() WHERE id = p_order_id;
    RETURN 'Заказ успешно оформлен.';
END;
$$ LANGUAGE plpgsql;

-- ---------------------------------------------------------------------
-- Представление: итоговая сумма счёта (вычисляется, не хранится).
-- Сумма счёта — производная от сумм входящих в него заказов, поэтому
-- не дублируется в таблице bills (соответствие 3НФ).
-- ---------------------------------------------------------------------
CREATE OR REPLACE VIEW v_bill_totals AS
SELECT b.id AS bill_id,
       COALESCE(SUM(vt.order_total), 0) AS total
FROM bills b
LEFT JOIN bill_orders bo ON bo.bill_id = b.id
LEFT JOIN v_order_totals vt ON vt.order_id = bo.order_id
GROUP BY b.id;

-- ---------------------------------------------------------------------
-- Представление: счёт с выведенными (не хранимыми) стол и сумма.
-- Стол берётся из заказов счёта, сумма — из v_bill_totals.
-- ---------------------------------------------------------------------
CREATE OR REPLACE VIEW v_bills AS
SELECT b.id,
       b.status,
       b.created_at,
       b.paid_at,
       tbl.table_id,
       t.number AS table_number,
       bt.total
FROM bills b
JOIN v_bill_totals bt ON bt.bill_id = b.id
LEFT JOIN LATERAL (
    SELECT MIN(o.table_id) AS table_id
    FROM bill_orders bo JOIN orders o ON o.id = bo.order_id
    WHERE bo.bill_id = b.id
) tbl ON TRUE
LEFT JOIN restaurant_tables t ON t.id = tbl.table_id;

-- =====================================================================
--  ОТЧЁТЫ (по требованиям раздела «Статистика»)
-- =====================================================================

-- 1) Продажи блюд за два месяца с динамикой.
--    Группировка по категориям, сортировка по названию блюда.
CREATE OR REPLACE FUNCTION rpt_dish_sales(
    p_year1 INT, p_month1 INT, p_year2 INT, p_month2 INT)
RETURNS TABLE (
    category   VARCHAR,
    dish       VARCHAR,
    qty_period1 BIGINT,
    qty_period2 BIGINT,
    dynamics    BIGINT
) AS $$
    WITH sold AS (
        SELECT d.category_id, oi.dish_id, oi.quantity,
               EXTRACT(YEAR  FROM o.placed_at)::INT  AS y,
               EXTRACT(MONTH FROM o.placed_at)::INT  AS m
        FROM order_items oi
        JOIN orders o ON o.id = oi.order_id
        JOIN dishes d ON d.id = oi.dish_id
        WHERE o.status = 'SERVED' AND o.placed_at IS NOT NULL
    )
    SELECT c.name AS category,
           d.name AS dish,
           COALESCE(SUM(s.quantity) FILTER (WHERE s.y = p_year1 AND s.m = p_month1), 0) AS qty_period1,
           COALESCE(SUM(s.quantity) FILTER (WHERE s.y = p_year2 AND s.m = p_month2), 0) AS qty_period2,
           COALESCE(SUM(s.quantity) FILTER (WHERE s.y = p_year2 AND s.m = p_month2), 0)
         - COALESCE(SUM(s.quantity) FILTER (WHERE s.y = p_year1 AND s.m = p_month1), 0) AS dynamics
    FROM dishes d
    JOIN categories c ON c.id = d.category_id
    LEFT JOIN sold s ON s.dish_id = d.id
    GROUP BY c.name, d.name
    HAVING COALESCE(SUM(s.quantity) FILTER (WHERE s.y = p_year1 AND s.m = p_month1), 0) > 0
        OR COALESCE(SUM(s.quantity) FILTER (WHERE s.y = p_year2 AND s.m = p_month2), 0) > 0
    ORDER BY c.name, d.name;
$$ LANGUAGE sql STABLE;

-- 2) Количество броней по столам за месяц.
--    Сортировка: по номеру стола, затем по количеству броней (популярность).
CREATE OR REPLACE FUNCTION rpt_table_bookings(p_year INT, p_month INT)
RETURNS TABLE (
    year_no   INT,
    month_no  INT,
    table_no  INT,
    bookings  BIGINT
) AS $$
    SELECT p_year, p_month, t.number, COUNT(rt.reservation_id)
    FROM restaurant_tables t
    LEFT JOIN reservation_tables rt ON rt.table_id = t.id
    LEFT JOIN reservations r ON r.id = rt.reservation_id
        AND EXTRACT(YEAR FROM r.reserve_date) = p_year
        AND EXTRACT(MONTH FROM r.reserve_date) = p_month
        AND r.status <> 'CANCELLED'
    GROUP BY t.number
    ORDER BY t.number, COUNT(rt.reservation_id) DESC;
$$ LANGUAGE sql STABLE;

-- 3) Работа официантов за два месяца с динамикой.
CREATE OR REPLACE FUNCTION rpt_waiter_stats(
    p_year1 INT, p_month1 INT, p_year2 INT, p_month2 INT)
RETURNS TABLE (
    last_name      VARCHAR,
    first_name     VARCHAR,
    middle_name    VARCHAR,
    orders_p1      BIGINT,
    receipts_p1    BIGINT,
    sum_p1         NUMERIC,
    orders_p2      BIGINT,
    receipts_p2    BIGINT,
    sum_p2         NUMERIC,
    d_orders       BIGINT,
    d_receipts     BIGINT,
    d_sum          NUMERIC
) AS $$
    -- Официант заказа и чека определяется через v_orders
    -- (закрепление стола за официантом в смене).
    WITH ord AS (
        SELECT vo.waiter_id,
               EXTRACT(YEAR FROM vo.placed_at)::INT y, EXTRACT(MONTH FROM vo.placed_at)::INT m,
               COUNT(*) cnt
        FROM v_orders vo
        WHERE vo.status <> 'CANCELLED' AND vo.placed_at IS NOT NULL AND vo.waiter_id IS NOT NULL
        GROUP BY vo.waiter_id, y, m
    ),
    rec AS (
        SELECT w.waiter_id,
               EXTRACT(YEAR FROM r.paid_at)::INT y, EXTRACT(MONTH FROM r.paid_at)::INT m,
               COUNT(*) cnt, SUM(r.total) total
        FROM receipts r
        JOIN LATERAL (
            SELECT vo.waiter_id
            FROM bill_orders bo JOIN v_orders vo ON vo.id = bo.order_id
            WHERE bo.bill_id = r.bill_id AND vo.waiter_id IS NOT NULL
            LIMIT 1
        ) w ON TRUE
        GROUP BY w.waiter_id, y, m
    )
    SELECT u.last_name, u.first_name, u.middle_name,
           COALESCE(o1.cnt,0), COALESCE(r1.cnt,0), COALESCE(r1.total,0),
           COALESCE(o2.cnt,0), COALESCE(r2.cnt,0), COALESCE(r2.total,0),
           COALESCE(o2.cnt,0)-COALESCE(o1.cnt,0),
           COALESCE(r2.cnt,0)-COALESCE(r1.cnt,0),
           COALESCE(r2.total,0)-COALESCE(r1.total,0)
    FROM users u
    JOIN roles ro ON ro.id = u.role_id AND ro.code = 'WAITER'
    LEFT JOIN ord o1 ON o1.waiter_id=u.id AND o1.y=p_year1 AND o1.m=p_month1
    LEFT JOIN ord o2 ON o2.waiter_id=u.id AND o2.y=p_year2 AND o2.m=p_month2
    LEFT JOIN rec r1 ON r1.waiter_id=u.id AND r1.y=p_year1 AND r1.m=p_month1
    LEFT JOIN rec r2 ON r2.waiter_id=u.id AND r2.y=p_year2 AND r2.m=p_month2
    ORDER BY u.last_name, u.first_name;
$$ LANGUAGE sql STABLE;

-- 4) Свободные столы на заданные дату и время.
CREATE OR REPLACE FUNCTION rpt_free_tables(p_date DATE, p_time TIME)
RETURNS TABLE (table_no INT, seats INT) AS $$
    SELECT t.number, t.seats
    FROM restaurant_tables t
    WHERE NOT EXISTS (
        SELECT 1
        FROM reservation_tables rt
        JOIN reservations r ON r.id = rt.reservation_id
        WHERE rt.table_id = t.id
          AND r.status = 'ACTIVE'
          AND r.reserve_date = p_date
          AND p_time >= r.start_time AND p_time < r.end_time
    )
    ORDER BY t.number;
$$ LANGUAGE sql STABLE;

-- 5) Общий список занятости столов на дату (свободные и занятые брони).
CREATE OR REPLACE FUNCTION rpt_tables_occupancy(p_date DATE)
RETURNS TABLE (
    on_date    DATE,
    start_time TIME,
    end_time   TIME,
    table_no   INT,
    reservation_no INT
) AS $$
    SELECT p_date, r.start_time, r.end_time, t.number, r.id
    FROM restaurant_tables t
    LEFT JOIN reservation_tables rt ON rt.table_id = t.id
    LEFT JOIN reservations r ON r.id = rt.reservation_id
        AND r.reserve_date = p_date AND r.status = 'ACTIVE'
    ORDER BY t.number, r.start_time;
$$ LANGUAGE sql STABLE;

-- 6) Почасовая занятость столов на дату (рабочий день 9:00–23:00).
--    Возвращает строки (стол, час, занят?). Сведение в таблицу — на стороне UI.
CREATE OR REPLACE FUNCTION rpt_hourly_occupancy(p_date DATE)
RETURNS TABLE (table_no INT, hour_no INT, busy BOOLEAN) AS $$
    SELECT t.number, h.hour_no,
           EXISTS (
               SELECT 1 FROM reservation_tables rt
               JOIN reservations r ON r.id = rt.reservation_id
               WHERE rt.table_id = t.id AND r.status = 'ACTIVE'
                 AND r.reserve_date = p_date
                 AND h.hour_no >= EXTRACT(HOUR FROM r.start_time)
                 AND h.hour_no <  EXTRACT(HOUR FROM r.end_time)
           )
    FROM restaurant_tables t
    CROSS JOIN generate_series(9, 22) AS h(hour_no)
    ORDER BY t.number, h.hour_no;
$$ LANGUAGE sql STABLE;

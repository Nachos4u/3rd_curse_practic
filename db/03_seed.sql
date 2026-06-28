-- =====================================================================
--  Прайм Бургер — наполнение демонстрационными данными
--  Файл 3/3
--  Текущая дата сценария: июнь 2026 (отчёты сравнивают май и июнь 2026)
-- =====================================================================
SET client_min_messages = WARNING;

TRUNCATE receipts, bill_orders, bills, order_items, orders,
         stock_movements, stock, promotion_dishes, promotions,
         dishes, categories, shift_tables, shifts,
         reservation_tables, reservations, restaurant_tables, users, roles
RESTART IDENTITY CASCADE;

-- Роли
INSERT INTO roles(id, code, name) VALUES
 (1,'ADMIN','Администратор'),
 (2,'WAITER','Официант'),
 (3,'KITCHEN','Кухня'),
 (4,'CLIENT','Клиент');

-- Пользователи (пароли: admin123 / waiter123 / kitchen123 / client123)
INSERT INTO users(login, password_hash, role_id, last_name, first_name, middle_name, phone) VALUES
 ('admin',   'PBKDF2$100000$tJtVRxWmfY3cV4BD08BjnQ==$ZfaPYj0DNlur+oc1WqwtZFVM5N+uO/TKtbeflRS0EEg=', 1, 'Администраторов','Админ','Админович','+70000000001'),
 ('ivanov',  'PBKDF2$100000$1dFf3f8nw0uNZA6am+bjmw==$HSbvTCcviokigQQwo9fW/OMIw4rXVm9FwLHxskzzsvU=', 2, 'Иванов','Иван','Иванович','+70000000002'),
 ('petrov',  'PBKDF2$100000$8S7B6R5+tf1oPaMSGQVJVQ==$BhCO8mnuxOwPxbC0QBvYCyBexmm0ltkZFM1bHD66BoQ=', 2, 'Петров','Пётр','Петрович','+70000000003'),
 ('kitchen', 'PBKDF2$100000$TtM5fV5X87h0MZ9j5uKx4A==$Zw7LpLvaNk7Z73gBtsV3ty9T6IuWhD+hv8yzRh1iKBc=', 3, 'Кухнин','Повар','Поварович','+70000000004'),
 ('client',  'PBKDF2$100000$KItrPa/+5n0wD22j17044Q==$VDgBPT81muOY8g4vEpYvWGOVu52qXxUvIJ9Izx8rT94=', 4, 'Сидоров','Сидор','Сидорович','+70000000005');

-- Столики (схема зала: pos_x, pos_y — условные координаты)
INSERT INTO restaurant_tables(number, seats, pos_x, pos_y) VALUES
 (1,4, 40, 40),(2,4,180,40),(3,2,320,40),(4,4,460,40),
 (5,4, 40,200),(6,4,180,200),(7,2,320,200),(8,4,460,200),
 (9,4, 40,360),(10,4,180,360);

-- Категории и блюда
INSERT INTO categories(name) VALUES ('Бургеры'),('Закуски'),('Напитки'),('Десерты');

INSERT INTO dishes(category_id, name, base_price) VALUES
 (1,'Прайм Бургер', 390.00),
 (1,'Чизбургер', 320.00),
 (1,'Двойной бургер', 470.00),
 (1,'Куриный бургер', 350.00),
 (2,'Картофель фри', 150.00),
 (2,'Луковые кольца', 180.00),
 (2,'Наггетсы', 220.00),
 (3,'Кола 0,5', 120.00),
 (3,'Лимонад', 140.00),
 (3,'Кофе', 130.00),
 (4,'Чизкейк', 250.00),
 (4,'Мороженое', 160.00);

-- Склад: остаток порций
INSERT INTO stock(dish_id, quantity)
 SELECT id, 100 FROM dishes;

-- Акция: скидка 15% на десерты (Чизкейк, Мороженое) в июне 2026.
-- Скидка на категорию задаётся перечислением её блюд.
INSERT INTO promotions(name, discount_percent, start_date, end_date)
 VALUES ('Сладкий июнь', 15.00, DATE '2026-06-01', DATE '2026-06-30');
INSERT INTO promotion_dishes(promotion_id, dish_id)
 SELECT (SELECT id FROM promotions WHERE name='Сладкий июнь'), d.id
 FROM dishes d JOIN categories c ON c.id = d.category_id
 WHERE c.name = 'Десерты';

-- ---------------------------------------------------------------------
-- Демонстрационные брони (май и июнь 2026). Бронь — на имя гостя.
-- ---------------------------------------------------------------------
INSERT INTO reservations(guest_name, guest_phone, guests_count, reserve_date, start_time, end_time, status) VALUES
 ('Сидоров Сидор',  '+70000000005', 3, DATE '2026-05-05', TIME '12:00', TIME '14:00', 'COMPLETED'),
 ('Сидоров Сидор',  '+70000000005', 4, DATE '2026-05-12', TIME '18:00', TIME '20:00', 'COMPLETED'),
 ('Кузнецова Анна', '+70000000010', 2, DATE '2026-05-20', TIME '13:00', TIME '15:00', 'COMPLETED'),
 ('Сидоров Сидор',  '+70000000005', 4, DATE '2026-06-03', TIME '19:00', TIME '21:00', 'COMPLETED'),
 ('Морозов Олег',   '+70000000011', 3, DATE '2026-06-10', TIME '12:00', TIME '14:00', 'COMPLETED'),
 ('Сидоров Сидор',  '+70000000005', 2, DATE '2026-06-22', TIME '13:00', TIME '15:00', 'ACTIVE'),
 ('Сидоров Сидор',  '+70000000005', 4, DATE '2026-06-22', TIME '19:00', TIME '21:00', 'ACTIVE');
INSERT INTO reservation_tables(reservation_id, table_id) VALUES
 (1,1),(2,2),(3,3),(4,1),(5,2),(6,4),(7,5),(7,6);

-- ---------------------------------------------------------------------
-- Вспомогательная функция: один оплаченный заказ.
-- Создаёт (при необходимости) смену официанта на дату заказа и закрепляет
-- за ним стол — именно по этому закреплению v_orders определяет официанта.
-- ---------------------------------------------------------------------
CREATE OR REPLACE FUNCTION seed_paid_order(
    p_waiter INT, p_table INT, p_when TIMESTAMP,
    p_dish1 INT, p_q1 INT, p_dish2 INT, p_q2 INT)
RETURNS VOID AS $$
DECLARE v_shift INT; v_order INT; v_bill INT; v_total NUMERIC;
BEGIN
    INSERT INTO shifts(waiter_id, work_date, status, opened_at, closed_at)
    VALUES (p_waiter, p_when::date, 'CLOSED',
            p_when::date + TIME '09:00', p_when::date + TIME '23:00')
    ON CONFLICT (waiter_id, work_date) DO UPDATE SET status = shifts.status
    RETURNING id INTO v_shift;
    INSERT INTO shift_tables(shift_id, table_id) VALUES (v_shift, p_table)
    ON CONFLICT DO NOTHING;

    INSERT INTO orders(table_id, status, created_at, placed_at)
    VALUES (p_table, 'SERVED', p_when, p_when) RETURNING id INTO v_order;
    INSERT INTO order_items(order_id, dish_id, quantity, unit_price, discount_percent)
    SELECT v_order, p_dish1, p_q1, base_price, 0 FROM dishes WHERE id = p_dish1;
    INSERT INTO order_items(order_id, dish_id, quantity, unit_price, discount_percent)
    SELECT v_order, p_dish2, p_q2, base_price, 0 FROM dishes WHERE id = p_dish2;
    UPDATE stock SET quantity = quantity - p_q1 WHERE dish_id = p_dish1;
    UPDATE stock SET quantity = quantity - p_q2 WHERE dish_id = p_dish2;

    INSERT INTO bills(status, created_at, paid_at) VALUES ('PAID', p_when, p_when) RETURNING id INTO v_bill;
    INSERT INTO bill_orders(bill_id, order_id) VALUES (v_bill, v_order);
    SELECT total INTO v_total FROM v_bill_totals WHERE bill_id = v_bill;
    INSERT INTO receipts(bill_id, total, payment_method, paid_at)
    VALUES (v_bill, v_total, 'CASH', p_when);
END;
$$ LANGUAGE plpgsql;

-- Май 2026 (официанты: 2 — Иванов, 3 — Петров)
SELECT seed_paid_order(2, 1, TIMESTAMP '2026-05-05 12:30', 1, 2, 5, 2);
SELECT seed_paid_order(3, 2, TIMESTAMP '2026-05-12 18:30', 2, 1, 8, 3);
SELECT seed_paid_order(2, 3, TIMESTAMP '2026-05-20 13:15', 3, 1, 11, 1);
SELECT seed_paid_order(3, 5, TIMESTAMP '2026-05-25 14:00', 4, 2, 9, 2);

-- Июнь 2026
SELECT seed_paid_order(2, 1, TIMESTAMP '2026-06-03 19:20', 1, 3, 7, 2);
SELECT seed_paid_order(3, 2, TIMESTAMP '2026-06-10 12:40', 2, 2, 11, 2);
SELECT seed_paid_order(2, 4, TIMESTAMP '2026-06-15 20:00', 3, 1, 12, 1);
SELECT seed_paid_order(3, 6, TIMESTAMP '2026-06-18 13:30', 1, 1, 8, 2);

DROP FUNCTION seed_paid_order(INT,INT,TIMESTAMP,INT,INT,INT,INT);

-- Открытая смена официанта Иванова на сегодня со столами 1–5
-- (нужна, чтобы официант мог вести заказы и чтобы определялся как официант).
INSERT INTO shifts(waiter_id, work_date, status, opened_at)
VALUES (2, CURRENT_DATE, 'OPEN', now())
ON CONFLICT (waiter_id, work_date) DO UPDATE SET status = 'OPEN', opened_at = now();
INSERT INTO shift_tables(shift_id, table_id)
SELECT s.id, t.id
FROM shifts s, restaurant_tables t
WHERE s.waiter_id = 2 AND s.work_date = CURRENT_DATE AND t.number IN (1,2,3,4,5)
ON CONFLICT DO NOTHING;

-- Один активный заказ в статусе «Составление» (стол 1 закреплён за Ивановым сегодня)
INSERT INTO orders(table_id, status, created_at)
VALUES (1, 'COMPOSING', now());

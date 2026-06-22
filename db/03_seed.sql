-- =====================================================================
--  Прайм Бургер — наполнение демонстрационными данными
--  Файл 3/3
--  Текущая дата сценария: 22.06.2026 (отчёты сравнивают май и июнь 2026)
-- =====================================================================
SET client_min_messages = WARNING;

TRUNCATE receipts, bill_orders, bills, order_items, orders,
         stock_movements, stock, promotion_dishes, promotion_categories, promotions,
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

-- Акция: скидка 15% на категорию «Десерты» в июне 2026
INSERT INTO promotions(name, discount_percent, start_date, end_date)
 VALUES ('Сладкий июнь', 15.00, DATE '2026-06-01', DATE '2026-06-30');
INSERT INTO promotion_categories(promotion_id, category_id)
 SELECT (SELECT id FROM promotions WHERE name='Сладкий июнь'), id FROM categories WHERE name='Десерты';

-- Смены официантов (план + закрытые) на текущую дату
INSERT INTO shifts(waiter_id, work_date, status, opened_at, closed_at) VALUES
 (2, DATE '2026-06-22', 'OPEN', TIMESTAMP '2026-06-22 09:00', NULL),
 (3, DATE '2026-06-21', 'CLOSED', TIMESTAMP '2026-06-21 09:00', TIMESTAMP '2026-06-21 23:00');
INSERT INTO shift_tables(shift_id, table_id) VALUES
 (1,1),(1,2),(1,3),(1,4),(1,5),
 (2,6),(2,7),(2,8),(2,9),(2,10);

-- ---------------------------------------------------------------------
-- Демонстрационные брони (май и июнь 2026)
-- ---------------------------------------------------------------------
INSERT INTO reservations(client_id, guests_count, reserve_date, start_time, end_time, status) VALUES
 (5, 3, DATE '2026-05-05', TIME '12:00', TIME '14:00', 'COMPLETED'),
 (5, 4, DATE '2026-05-12', TIME '18:00', TIME '20:00', 'COMPLETED'),
 (5, 2, DATE '2026-05-20', TIME '13:00', TIME '15:00', 'COMPLETED'),
 (5, 4, DATE '2026-06-03', TIME '19:00', TIME '21:00', 'COMPLETED'),
 (5, 3, DATE '2026-06-10', TIME '12:00', TIME '14:00', 'COMPLETED'),
 (5, 2, DATE '2026-06-22', TIME '13:00', TIME '15:00', 'ACTIVE'),
 (5, 4, DATE '2026-06-22', TIME '19:00', TIME '21:00', 'ACTIVE');
INSERT INTO reservation_tables(reservation_id, table_id) VALUES
 (1,1),(2,2),(3,3),(4,1),(5,2),(6,4),(7,5),(7,6);

-- ---------------------------------------------------------------------
-- Демонстрационные заказы (SERVED) с позициями, счетами и чеками.
-- Процедура: создаём заказ -> позиции -> счёт -> чек.
-- Здесь данные вставляются напрямую (без вызова функций) для скорости наполнения.
-- ---------------------------------------------------------------------

-- Вспомогательная функция наполнения одного оплаченного заказа
CREATE OR REPLACE FUNCTION seed_paid_order(
    p_resv INT, p_table INT, p_waiter INT, p_when TIMESTAMP,
    p_dish1 INT, p_q1 INT, p_dish2 INT, p_q2 INT)
RETURNS VOID AS $$
DECLARE v_order INT; v_bill INT; v_total NUMERIC;
BEGIN
    INSERT INTO orders(reservation_id, table_id, waiter_id, status, created_at, placed_at)
    VALUES (p_resv, p_table, p_waiter, 'SERVED', p_when, p_when) RETURNING id INTO v_order;

    INSERT INTO order_items(order_id, dish_id, quantity, unit_price, discount_percent)
    SELECT v_order, p_dish1, p_q1, base_price, 0 FROM dishes WHERE id=p_dish1;
    INSERT INTO order_items(order_id, dish_id, quantity, unit_price, discount_percent)
    SELECT v_order, p_dish2, p_q2, base_price, 0 FROM dishes WHERE id=p_dish2;

    UPDATE stock SET quantity = quantity - p_q1 WHERE dish_id=p_dish1;
    UPDATE stock SET quantity = quantity - p_q2 WHERE dish_id=p_dish2;

    INSERT INTO bills(reservation_id, table_id, status, created_at, paid_at)
    VALUES (p_resv, p_table, 'PAID', p_when, p_when) RETURNING id INTO v_bill;
    INSERT INTO bill_orders(bill_id, order_id) VALUES (v_bill, v_order);

    SELECT total_amount INTO v_total FROM bills WHERE id=v_bill;
    INSERT INTO receipts(bill_id, waiter_id, total, payment_method, paid_at)
    VALUES (v_bill, p_waiter, v_total, 'CASH', p_when);
END;
$$ LANGUAGE plpgsql;

-- Май 2026
SELECT seed_paid_order(1, 1, 2, TIMESTAMP '2026-05-05 12:30', 1, 2, 5, 2);
SELECT seed_paid_order(2, 2, 3, TIMESTAMP '2026-05-12 18:30', 2, 1, 8, 3);
SELECT seed_paid_order(3, 3, 2, TIMESTAMP '2026-05-20 13:15', 3, 1, 11, 1);
SELECT seed_paid_order(NULL, 5, 3, TIMESTAMP '2026-05-25 14:00', 4, 2, 9, 2);

-- Июнь 2026
SELECT seed_paid_order(4, 1, 2, TIMESTAMP '2026-06-03 19:20', 1, 3, 7, 2);
SELECT seed_paid_order(5, 2, 3, TIMESTAMP '2026-06-10 12:40', 2, 2, 11, 2);
SELECT seed_paid_order(NULL, 4, 2, TIMESTAMP '2026-06-15 20:00', 3, 1, 12, 1);
SELECT seed_paid_order(NULL, 6, 3, TIMESTAMP '2026-06-18 13:30', 1, 1, 8, 2);

DROP FUNCTION seed_paid_order(INT,INT,INT,TIMESTAMP,INT,INT,INT,INT);

-- Один активный заказ в статусе «Составление» для демонстрации UI
INSERT INTO orders(reservation_id, table_id, waiter_id, status, created_at)
VALUES (6, 4, 2, 'COMPOSING', now());

-- =====================================================================
--  Прайм Бургер — схема базы данных (PostgreSQL)
--  Учебная практика УП 02.01
--  Файл 1/3: таблицы, ключи, ограничения (нормализация до 3НФ)
-- =====================================================================

-- Создание схемы выполнять под суперпользователем postgres.
-- Все объекты размещаются в схеме public базы restaurant_db.

SET client_min_messages = WARNING;

-- ---------------------------------------------------------------------
-- Перечисления (ENUM) — фиксированные наборы значений статусов.
-- ---------------------------------------------------------------------
DROP TYPE IF EXISTS order_status   CASCADE;
DROP TYPE IF EXISTS table_status   CASCADE;
DROP TYPE IF EXISTS reserve_status CASCADE;
DROP TYPE IF EXISTS shift_status   CASCADE;
DROP TYPE IF EXISTS bill_status    CASCADE;

-- Статусы заказа (стороны зала и кухни объединены в один жизненный цикл).
CREATE TYPE order_status AS ENUM (
    'COMPOSING',   -- Составление (содержимое можно менять)
    'PLACED',      -- Оформлен (зафиксированы дата/время, менять нельзя)
    'CANCELLED',   -- Отменён
    'COOKING',     -- Готовится (статус кухни)
    'READY',       -- Готов к выдаче / Принят на выдачу
    'SERVED'       -- Выдан клиенту
);

-- Статусы столика.
CREATE TYPE table_status AS ENUM (
    'FREE',        -- Свободен
    'RESERVED',    -- Забронирован
    'OCCUPIED'     -- Занят
);

-- Статусы брони.
CREATE TYPE reserve_status AS ENUM (
    'ACTIVE',      -- Активна
    'CANCELLED',   -- Отменена
    'COMPLETED'    -- Завершена (гости обслужены)
);

-- Статусы смены официанта.
CREATE TYPE shift_status AS ENUM (
    'PLANNED',     -- Запланирована администратором
    'OPEN',        -- Открыта официантом
    'CLOSED'       -- Закрыта официантом
);

-- Статусы счёта.
CREATE TYPE bill_status AS ENUM (
    'OPEN',        -- Открыт (формируется)
    'PAID',        -- Оплачен
    'CANCELLED'    -- Отменён
);

-- ---------------------------------------------------------------------
-- Роли и пользователи
-- ---------------------------------------------------------------------
CREATE TABLE roles (
    id    SMALLINT     PRIMARY KEY,
    code  VARCHAR(20)  NOT NULL UNIQUE,   -- ADMIN / WAITER / KITCHEN / CLIENT
    name  VARCHAR(50)  NOT NULL
);

CREATE TABLE users (
    id            SERIAL       PRIMARY KEY,
    login         VARCHAR(50)  NOT NULL UNIQUE,
    password_hash VARCHAR(200) NOT NULL,     -- PBKDF2 (соль хранится внутри строки)
    role_id       SMALLINT     NOT NULL REFERENCES roles(id),
    last_name     VARCHAR(60)  NOT NULL,
    first_name    VARCHAR(60)  NOT NULL,
    middle_name   VARCHAR(60),
    phone         VARCHAR(20),
    is_active     BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at    TIMESTAMP    NOT NULL DEFAULT now()
);

CREATE INDEX idx_users_role ON users(role_id);

-- ---------------------------------------------------------------------
-- Столики и схема зала
-- ---------------------------------------------------------------------
CREATE TABLE restaurant_tables (
    id      SERIAL    PRIMARY KEY,
    number  INTEGER   NOT NULL UNIQUE,            -- номер стола в зале
    seats   SMALLINT  NOT NULL DEFAULT 4 CHECK (seats BETWEEN 1 AND 4),
    pos_x   INTEGER   NOT NULL DEFAULT 0,         -- координаты для схемы зала
    pos_y   INTEGER   NOT NULL DEFAULT 0,
    status  table_status NOT NULL DEFAULT 'FREE'
);

-- ---------------------------------------------------------------------
-- Брони (бронь может включать несколько столов — связь M:N)
-- ---------------------------------------------------------------------
CREATE TABLE reservations (
    id           SERIAL         PRIMARY KEY,
    client_id    INTEGER        REFERENCES users(id),   -- кто бронирует (может быть NULL для гостя «с улицы»)
    guests_count SMALLINT       NOT NULL CHECK (guests_count > 0),
    reserve_date DATE           NOT NULL,
    start_time   TIME           NOT NULL,
    end_time     TIME           NOT NULL,
    status       reserve_status NOT NULL DEFAULT 'ACTIVE',
    created_at   TIMESTAMP      NOT NULL DEFAULT now(),
    CHECK (end_time > start_time)
);

CREATE TABLE reservation_tables (
    reservation_id INTEGER NOT NULL REFERENCES reservations(id) ON DELETE CASCADE,
    table_id       INTEGER NOT NULL REFERENCES restaurant_tables(id),
    PRIMARY KEY (reservation_id, table_id)
);

CREATE INDEX idx_resv_date ON reservations(reserve_date);

-- ---------------------------------------------------------------------
-- Смены официантов и прикрепление столов к смене
-- ---------------------------------------------------------------------
CREATE TABLE shifts (
    id         SERIAL       PRIMARY KEY,
    waiter_id  INTEGER      NOT NULL REFERENCES users(id),
    work_date  DATE         NOT NULL,
    status     shift_status NOT NULL DEFAULT 'PLANNED',
    opened_at  TIMESTAMP,
    closed_at  TIMESTAMP,
    UNIQUE (waiter_id, work_date)
);

CREATE TABLE shift_tables (
    shift_id INTEGER NOT NULL REFERENCES shifts(id) ON DELETE CASCADE,
    table_id INTEGER NOT NULL REFERENCES restaurant_tables(id),
    PRIMARY KEY (shift_id, table_id)
);

CREATE INDEX idx_shifts_date ON shifts(work_date);

-- ---------------------------------------------------------------------
-- Меню: категории, блюда, склад, акции
-- ---------------------------------------------------------------------
CREATE TABLE categories (
    id   SERIAL      PRIMARY KEY,
    name VARCHAR(80) NOT NULL UNIQUE
);

CREATE TABLE dishes (
    id          SERIAL        PRIMARY KEY,
    category_id INTEGER       NOT NULL REFERENCES categories(id),
    name        VARCHAR(120)  NOT NULL,
    base_price  NUMERIC(10,2) NOT NULL CHECK (base_price >= 0),
    is_active   BOOLEAN       NOT NULL DEFAULT TRUE,
    UNIQUE (category_id, name)
);

-- «Склад» кухни: остаток порций по каждому блюду.
CREATE TABLE stock (
    dish_id  INTEGER PRIMARY KEY REFERENCES dishes(id),
    quantity INTEGER NOT NULL DEFAULT 0 CHECK (quantity >= 0)
);

-- Журнал движений склада (аудит списаний/возвратов).
CREATE TABLE stock_movements (
    id         SERIAL    PRIMARY KEY,
    dish_id    INTEGER   NOT NULL REFERENCES dishes(id),
    change     INTEGER   NOT NULL,           -- +возврат / -списание
    reason     VARCHAR(120) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT now()
);

-- Акции (скидки на блюда и/или категории на период).
CREATE TABLE promotions (
    id               SERIAL       PRIMARY KEY,
    name             VARCHAR(120) NOT NULL,
    discount_percent NUMERIC(5,2) NOT NULL CHECK (discount_percent BETWEEN 0 AND 100),
    start_date       DATE         NOT NULL,
    end_date         DATE         NOT NULL,
    is_active        BOOLEAN      NOT NULL DEFAULT TRUE,
    CHECK (end_date >= start_date)
);

CREATE TABLE promotion_dishes (
    promotion_id INTEGER NOT NULL REFERENCES promotions(id) ON DELETE CASCADE,
    dish_id      INTEGER NOT NULL REFERENCES dishes(id),
    PRIMARY KEY (promotion_id, dish_id)
);

CREATE TABLE promotion_categories (
    promotion_id INTEGER NOT NULL REFERENCES promotions(id) ON DELETE CASCADE,
    category_id  INTEGER NOT NULL REFERENCES categories(id),
    PRIMARY KEY (promotion_id, category_id)
);

-- ---------------------------------------------------------------------
-- Заказы и позиции заказа
-- ---------------------------------------------------------------------
CREATE TABLE orders (
    id             SERIAL       PRIMARY KEY,
    reservation_id INTEGER      REFERENCES reservations(id),
    table_id       INTEGER      NOT NULL REFERENCES restaurant_tables(id),
    waiter_id      INTEGER      NOT NULL REFERENCES users(id),
    status         order_status NOT NULL DEFAULT 'COMPOSING',
    created_at     TIMESTAMP    NOT NULL DEFAULT now(),
    placed_at      TIMESTAMP                                   -- момент оформления
);

CREATE TABLE order_items (
    id               SERIAL        PRIMARY KEY,
    order_id         INTEGER       NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    dish_id          INTEGER       NOT NULL REFERENCES dishes(id),
    quantity         INTEGER       NOT NULL CHECK (quantity > 0),
    unit_price       NUMERIC(10,2) NOT NULL CHECK (unit_price >= 0),  -- цена на момент добавления
    discount_percent NUMERIC(5,2)  NOT NULL DEFAULT 0 CHECK (discount_percent BETWEEN 0 AND 100),
    UNIQUE (order_id, dish_id)
);

CREATE INDEX idx_orders_waiter ON orders(waiter_id);
CREATE INDEX idx_orders_status ON orders(status);
CREATE INDEX idx_items_dish    ON order_items(dish_id);

-- ---------------------------------------------------------------------
-- Счета и чеки
-- ---------------------------------------------------------------------
CREATE TABLE bills (
    id             SERIAL        PRIMARY KEY,
    reservation_id INTEGER       REFERENCES reservations(id),
    table_id       INTEGER       NOT NULL REFERENCES restaurant_tables(id),
    status         bill_status   NOT NULL DEFAULT 'OPEN',
    total_amount   NUMERIC(12,2) NOT NULL DEFAULT 0,
    created_at     TIMESTAMP     NOT NULL DEFAULT now(),
    paid_at        TIMESTAMP
);

-- Счёт может объединять несколько заказов клиента (связь M:N).
CREATE TABLE bill_orders (
    bill_id  INTEGER NOT NULL REFERENCES bills(id) ON DELETE CASCADE,
    order_id INTEGER NOT NULL REFERENCES orders(id),
    PRIMARY KEY (bill_id, order_id)
);

CREATE TABLE receipts (
    id             SERIAL        PRIMARY KEY,
    bill_id        INTEGER       NOT NULL UNIQUE REFERENCES bills(id),
    waiter_id      INTEGER       NOT NULL REFERENCES users(id),
    total          NUMERIC(12,2) NOT NULL CHECK (total >= 0),
    payment_method VARCHAR(20)   NOT NULL DEFAULT 'CASH',
    paid_at        TIMESTAMP     NOT NULL DEFAULT now()
);

CREATE INDEX idx_receipts_waiter ON receipts(waiter_id);
CREATE INDEX idx_receipts_paid   ON receipts(paid_at);

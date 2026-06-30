--
-- PostgreSQL database dump
--

\restrict vN0ae3BdV7BA88hFIDO2VHkC0NKDSLtNiYqLzBevgNV5qUAufKOKLdXyQYhqeqM

-- Dumped from database version 18.4
-- Dumped by pg_dump version 18.4

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

DROP DATABASE IF EXISTS restaurant_db;
--
-- Name: restaurant_db; Type: DATABASE; Schema: -; Owner: -
--

CREATE DATABASE restaurant_db WITH TEMPLATE = template0 ENCODING = 'UTF8' LOCALE_PROVIDER = libc LOCALE = 'Russian_Russia.932';


\unrestrict vN0ae3BdV7BA88hFIDO2VHkC0NKDSLtNiYqLzBevgNV5qUAufKOKLdXyQYhqeqM
\connect restaurant_db
\restrict vN0ae3BdV7BA88hFIDO2VHkC0NKDSLtNiYqLzBevgNV5qUAufKOKLdXyQYhqeqM

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: bill_status; Type: TYPE; Schema: public; Owner: -
--

CREATE TYPE public.bill_status AS ENUM (
    'OPEN',
    'PAID',
    'CANCELLED'
);


--
-- Name: order_status; Type: TYPE; Schema: public; Owner: -
--

CREATE TYPE public.order_status AS ENUM (
    'COMPOSING',
    'PLACED',
    'CANCELLED',
    'COOKING',
    'READY',
    'SERVED'
);


--
-- Name: reserve_status; Type: TYPE; Schema: public; Owner: -
--

CREATE TYPE public.reserve_status AS ENUM (
    'ACTIVE',
    'CANCELLED',
    'COMPLETED'
);


--
-- Name: shift_status; Type: TYPE; Schema: public; Owner: -
--

CREATE TYPE public.shift_status AS ENUM (
    'PLANNED',
    'OPEN',
    'CLOSED'
);


--
-- Name: table_status; Type: TYPE; Schema: public; Owner: -
--

CREATE TYPE public.table_status AS ENUM (
    'FREE',
    'RESERVED',
    'OCCUPIED'
);


--
-- Name: fn_add_order_item(integer, integer, integer); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.fn_add_order_item(p_order_id integer, p_dish_id integer, p_qty integer) RETURNS text
    LANGUAGE plpgsql
    AS $$
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
$$;


--
-- Name: fn_effective_discount(integer, date); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.fn_effective_discount(p_dish_id integer, p_on_date date) RETURNS numeric
    LANGUAGE sql STABLE
    AS $$
    SELECT COALESCE(MAX(p.discount_percent), 0)
    FROM promotions p
    JOIN promotion_dishes pd ON pd.promotion_id = p.id
    WHERE p.is_active
      AND p_on_date BETWEEN p.start_date AND p.end_date
      AND pd.dish_id = p_dish_id;
$$;


--
-- Name: fn_place_order(integer); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.fn_place_order(p_order_id integer) RETURNS text
    LANGUAGE plpgsql
    AS $$
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
$$;


--
-- Name: fn_remove_order_item(integer, integer); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.fn_remove_order_item(p_order_id integer, p_dish_id integer) RETURNS text
    LANGUAGE plpgsql
    AS $$
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
$$;


--
-- Name: rpt_dish_sales(integer, integer, integer, integer); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.rpt_dish_sales(p_year1 integer, p_month1 integer, p_year2 integer, p_month2 integer) RETURNS TABLE(category character varying, dish character varying, qty_period1 bigint, qty_period2 bigint, dynamics bigint)
    LANGUAGE sql STABLE
    AS $$
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
$$;


--
-- Name: rpt_free_tables(date, time without time zone); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.rpt_free_tables(p_date date, p_time time without time zone) RETURNS TABLE(table_no integer, seats integer)
    LANGUAGE sql STABLE
    AS $$
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
$$;


--
-- Name: rpt_hourly_occupancy(date); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.rpt_hourly_occupancy(p_date date) RETURNS TABLE(table_no integer, hour_no integer, busy boolean)
    LANGUAGE sql STABLE
    AS $$
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
$$;


--
-- Name: rpt_table_bookings(integer, integer); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.rpt_table_bookings(p_year integer, p_month integer) RETURNS TABLE(year_no integer, month_no integer, table_no integer, bookings bigint)
    LANGUAGE sql STABLE
    AS $$
    SELECT p_year, p_month, t.number, COUNT(rt.reservation_id)
    FROM restaurant_tables t
    LEFT JOIN reservation_tables rt ON rt.table_id = t.id
    LEFT JOIN reservations r ON r.id = rt.reservation_id
        AND EXTRACT(YEAR FROM r.reserve_date) = p_year
        AND EXTRACT(MONTH FROM r.reserve_date) = p_month
        AND r.status <> 'CANCELLED'
    GROUP BY t.number
    ORDER BY t.number, COUNT(rt.reservation_id) DESC;
$$;


--
-- Name: rpt_tables_occupancy(date); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.rpt_tables_occupancy(p_date date) RETURNS TABLE(on_date date, start_time time without time zone, end_time time without time zone, table_no integer, reservation_no integer)
    LANGUAGE sql STABLE
    AS $$
    SELECT p_date, r.start_time, r.end_time, t.number, r.id
    FROM restaurant_tables t
    LEFT JOIN reservation_tables rt ON rt.table_id = t.id
    LEFT JOIN reservations r ON r.id = rt.reservation_id
        AND r.reserve_date = p_date AND r.status = 'ACTIVE'
    ORDER BY t.number, r.start_time;
$$;


--
-- Name: rpt_waiter_stats(integer, integer, integer, integer); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.rpt_waiter_stats(p_year1 integer, p_month1 integer, p_year2 integer, p_month2 integer) RETURNS TABLE(last_name character varying, first_name character varying, middle_name character varying, orders_p1 bigint, receipts_p1 bigint, sum_p1 numeric, orders_p2 bigint, receipts_p2 bigint, sum_p2 numeric, d_orders bigint, d_receipts bigint, d_sum numeric)
    LANGUAGE sql STABLE
    AS $$
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
$$;


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: bill_orders; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.bill_orders (
    bill_id integer NOT NULL,
    order_id integer NOT NULL
);


--
-- Name: bills; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.bills (
    id integer NOT NULL,
    status public.bill_status DEFAULT 'OPEN'::public.bill_status NOT NULL,
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    paid_at timestamp without time zone
);


--
-- Name: bills_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.bills_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: bills_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.bills_id_seq OWNED BY public.bills.id;


--
-- Name: categories; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.categories (
    id integer NOT NULL,
    name character varying(80) NOT NULL
);


--
-- Name: categories_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.categories_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: categories_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.categories_id_seq OWNED BY public.categories.id;


--
-- Name: dishes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.dishes (
    id integer NOT NULL,
    category_id integer NOT NULL,
    name character varying(120) NOT NULL,
    base_price numeric(10,2) NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    CONSTRAINT dishes_base_price_check CHECK ((base_price >= (0)::numeric))
);


--
-- Name: dishes_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.dishes_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: dishes_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.dishes_id_seq OWNED BY public.dishes.id;


--
-- Name: order_items; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.order_items (
    id integer NOT NULL,
    order_id integer NOT NULL,
    dish_id integer NOT NULL,
    quantity integer NOT NULL,
    unit_price numeric(10,2) NOT NULL,
    discount_percent numeric(5,2) DEFAULT 0 NOT NULL,
    CONSTRAINT order_items_discount_percent_check CHECK (((discount_percent >= (0)::numeric) AND (discount_percent <= (100)::numeric))),
    CONSTRAINT order_items_quantity_check CHECK ((quantity > 0)),
    CONSTRAINT order_items_unit_price_check CHECK ((unit_price >= (0)::numeric))
);


--
-- Name: order_items_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.order_items_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: order_items_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.order_items_id_seq OWNED BY public.order_items.id;


--
-- Name: orders; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.orders (
    id integer NOT NULL,
    table_id integer NOT NULL,
    status public.order_status DEFAULT 'COMPOSING'::public.order_status NOT NULL,
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    placed_at timestamp without time zone
);


--
-- Name: orders_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.orders_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: orders_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.orders_id_seq OWNED BY public.orders.id;


--
-- Name: promotion_dishes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.promotion_dishes (
    promotion_id integer NOT NULL,
    dish_id integer NOT NULL
);


--
-- Name: promotions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.promotions (
    id integer NOT NULL,
    name character varying(120) NOT NULL,
    discount_percent numeric(5,2) NOT NULL,
    start_date date NOT NULL,
    end_date date NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    CONSTRAINT promotions_check CHECK ((end_date >= start_date)),
    CONSTRAINT promotions_discount_percent_check CHECK (((discount_percent >= (0)::numeric) AND (discount_percent <= (100)::numeric)))
);


--
-- Name: promotions_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.promotions_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: promotions_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.promotions_id_seq OWNED BY public.promotions.id;


--
-- Name: receipts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.receipts (
    id integer NOT NULL,
    bill_id integer NOT NULL,
    total numeric(12,2) NOT NULL,
    payment_method character varying(20) DEFAULT 'CASH'::character varying NOT NULL,
    paid_at timestamp without time zone DEFAULT now() NOT NULL,
    CONSTRAINT receipts_total_check CHECK ((total >= (0)::numeric))
);


--
-- Name: receipts_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.receipts_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: receipts_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.receipts_id_seq OWNED BY public.receipts.id;


--
-- Name: reservation_tables; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.reservation_tables (
    reservation_id integer NOT NULL,
    table_id integer NOT NULL
);


--
-- Name: reservations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.reservations (
    id integer NOT NULL,
    guest_name character varying(120) NOT NULL,
    guest_phone character varying(20),
    guests_count smallint NOT NULL,
    reserve_date date NOT NULL,
    start_time time without time zone NOT NULL,
    end_time time without time zone NOT NULL,
    status public.reserve_status DEFAULT 'ACTIVE'::public.reserve_status NOT NULL,
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    CONSTRAINT reservations_check CHECK ((end_time > start_time)),
    CONSTRAINT reservations_guests_count_check CHECK ((guests_count > 0))
);


--
-- Name: reservations_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.reservations_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: reservations_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.reservations_id_seq OWNED BY public.reservations.id;


--
-- Name: restaurant_tables; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.restaurant_tables (
    id integer NOT NULL,
    number integer NOT NULL,
    seats smallint DEFAULT 4 NOT NULL,
    pos_x integer DEFAULT 0 NOT NULL,
    pos_y integer DEFAULT 0 NOT NULL,
    status public.table_status DEFAULT 'FREE'::public.table_status NOT NULL,
    CONSTRAINT restaurant_tables_seats_check CHECK (((seats >= 1) AND (seats <= 4)))
);


--
-- Name: restaurant_tables_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.restaurant_tables_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: restaurant_tables_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.restaurant_tables_id_seq OWNED BY public.restaurant_tables.id;


--
-- Name: roles; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.roles (
    id smallint NOT NULL,
    code character varying(20) NOT NULL,
    name character varying(50) NOT NULL
);


--
-- Name: shift_tables; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.shift_tables (
    shift_id integer NOT NULL,
    table_id integer NOT NULL
);


--
-- Name: shifts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.shifts (
    id integer NOT NULL,
    waiter_id integer NOT NULL,
    work_date date NOT NULL,
    status public.shift_status DEFAULT 'PLANNED'::public.shift_status NOT NULL,
    opened_at timestamp without time zone,
    closed_at timestamp without time zone
);


--
-- Name: shifts_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.shifts_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: shifts_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.shifts_id_seq OWNED BY public.shifts.id;


--
-- Name: stock; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.stock (
    dish_id integer NOT NULL,
    quantity integer DEFAULT 0 NOT NULL,
    CONSTRAINT stock_quantity_check CHECK ((quantity >= 0))
);


--
-- Name: stock_movements; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.stock_movements (
    id integer NOT NULL,
    dish_id integer NOT NULL,
    change integer NOT NULL,
    reason character varying(120) NOT NULL,
    created_at timestamp without time zone DEFAULT now() NOT NULL
);


--
-- Name: stock_movements_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.stock_movements_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: stock_movements_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.stock_movements_id_seq OWNED BY public.stock_movements.id;


--
-- Name: users; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.users (
    id integer NOT NULL,
    login character varying(50) NOT NULL,
    password_hash character varying(200) NOT NULL,
    role_id smallint NOT NULL,
    last_name character varying(60) NOT NULL,
    first_name character varying(60) NOT NULL,
    middle_name character varying(60),
    phone character varying(20),
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp without time zone DEFAULT now() NOT NULL
);


--
-- Name: users_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.users_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: users_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.users_id_seq OWNED BY public.users.id;


--
-- Name: v_order_items; Type: VIEW; Schema: public; Owner: -
--

CREATE VIEW public.v_order_items AS
 SELECT oi.id,
    oi.order_id,
    d.name AS dish_name,
    c.name AS category_name,
    oi.quantity,
    oi.unit_price,
    oi.discount_percent,
    round((((oi.quantity)::numeric * oi.unit_price) * ((1)::numeric - (oi.discount_percent / 100.0))), 2) AS line_total
   FROM ((public.order_items oi
     JOIN public.dishes d ON ((d.id = oi.dish_id)))
     JOIN public.categories c ON ((c.id = d.category_id)));


--
-- Name: v_order_totals; Type: VIEW; Schema: public; Owner: -
--

CREATE VIEW public.v_order_totals AS
 SELECT o.id AS order_id,
    o.status,
    o.table_id,
    COALESCE(sum(vi.line_total), (0)::numeric) AS order_total,
    COALESCE(sum(vi.quantity), (0)::bigint) AS items_count
   FROM (public.orders o
     LEFT JOIN public.v_order_items vi ON ((vi.order_id = o.id)))
  GROUP BY o.id, o.status, o.table_id;


--
-- Name: v_bill_totals; Type: VIEW; Schema: public; Owner: -
--

CREATE VIEW public.v_bill_totals AS
 SELECT b.id AS bill_id,
    COALESCE(sum(vt.order_total), (0)::numeric) AS total
   FROM ((public.bills b
     LEFT JOIN public.bill_orders bo ON ((bo.bill_id = b.id)))
     LEFT JOIN public.v_order_totals vt ON ((vt.order_id = bo.order_id)))
  GROUP BY b.id;


--
-- Name: v_bills; Type: VIEW; Schema: public; Owner: -
--

CREATE VIEW public.v_bills AS
 SELECT b.id,
    b.status,
    b.created_at,
    b.paid_at,
    tbl.table_id,
    t.number AS table_number,
    bt.total
   FROM (((public.bills b
     JOIN public.v_bill_totals bt ON ((bt.bill_id = b.id)))
     LEFT JOIN LATERAL ( SELECT min(o.table_id) AS table_id
           FROM (public.bill_orders bo
             JOIN public.orders o ON ((o.id = bo.order_id)))
          WHERE (bo.bill_id = b.id)) tbl ON (true))
     LEFT JOIN public.restaurant_tables t ON ((t.id = tbl.table_id)));


--
-- Name: v_orders; Type: VIEW; Schema: public; Owner: -
--

CREATE VIEW public.v_orders AS
 SELECT o.id,
    o.table_id,
    t.number AS table_number,
    asg.waiter_id,
    (((u.last_name)::text || ' '::text) || (u.first_name)::text) AS waiter_name,
    o.status,
    o.created_at,
    o.placed_at,
    vt.order_total,
    vt.items_count
   FROM ((((public.orders o
     JOIN public.restaurant_tables t ON ((t.id = o.table_id)))
     JOIN public.v_order_totals vt ON ((vt.order_id = o.id)))
     LEFT JOIN LATERAL ( SELECT s.waiter_id
           FROM (public.shift_tables st
             JOIN public.shifts s ON ((s.id = st.shift_id)))
          WHERE ((st.table_id = o.table_id) AND (s.work_date = (o.created_at)::date))
         LIMIT 1) asg ON (true))
     LEFT JOIN public.users u ON ((u.id = asg.waiter_id)));


--
-- Name: bills id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.bills ALTER COLUMN id SET DEFAULT nextval('public.bills_id_seq'::regclass);


--
-- Name: categories id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.categories ALTER COLUMN id SET DEFAULT nextval('public.categories_id_seq'::regclass);


--
-- Name: dishes id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.dishes ALTER COLUMN id SET DEFAULT nextval('public.dishes_id_seq'::regclass);


--
-- Name: order_items id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.order_items ALTER COLUMN id SET DEFAULT nextval('public.order_items_id_seq'::regclass);


--
-- Name: orders id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.orders ALTER COLUMN id SET DEFAULT nextval('public.orders_id_seq'::regclass);


--
-- Name: promotions id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.promotions ALTER COLUMN id SET DEFAULT nextval('public.promotions_id_seq'::regclass);


--
-- Name: receipts id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.receipts ALTER COLUMN id SET DEFAULT nextval('public.receipts_id_seq'::regclass);


--
-- Name: reservations id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.reservations ALTER COLUMN id SET DEFAULT nextval('public.reservations_id_seq'::regclass);


--
-- Name: restaurant_tables id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.restaurant_tables ALTER COLUMN id SET DEFAULT nextval('public.restaurant_tables_id_seq'::regclass);


--
-- Name: shifts id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.shifts ALTER COLUMN id SET DEFAULT nextval('public.shifts_id_seq'::regclass);


--
-- Name: stock_movements id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.stock_movements ALTER COLUMN id SET DEFAULT nextval('public.stock_movements_id_seq'::regclass);


--
-- Name: users id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users ALTER COLUMN id SET DEFAULT nextval('public.users_id_seq'::regclass);


--
-- Data for Name: bill_orders; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.bill_orders (bill_id, order_id) FROM stdin;
1	1
2	2
3	3
4	4
5	5
6	6
7	7
8	8
\.


--
-- Data for Name: bills; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.bills (id, status, created_at, paid_at) FROM stdin;
1	PAID	2026-05-05 12:30:00	2026-05-05 12:30:00
2	PAID	2026-05-12 18:30:00	2026-05-12 18:30:00
3	PAID	2026-05-20 13:15:00	2026-05-20 13:15:00
4	PAID	2026-05-25 14:00:00	2026-05-25 14:00:00
5	PAID	2026-06-03 19:20:00	2026-06-03 19:20:00
6	PAID	2026-06-10 12:40:00	2026-06-10 12:40:00
7	PAID	2026-06-15 20:00:00	2026-06-15 20:00:00
8	PAID	2026-06-18 13:30:00	2026-06-18 13:30:00
\.


--
-- Data for Name: categories; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.categories (id, name) FROM stdin;
1	Бургеры
2	Закуски
3	Напитки
4	Десерты
\.


--
-- Data for Name: dishes; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.dishes (id, category_id, name, base_price, is_active) FROM stdin;
1	1	Прайм Бургер	390.00	t
2	1	Чизбургер	320.00	t
3	1	Двойной бургер	470.00	t
4	1	Куриный бургер	350.00	t
5	2	Картофель фри	150.00	t
6	2	Луковые кольца	180.00	t
7	2	Наггетсы	220.00	t
8	3	Кола 0,5	120.00	t
9	3	Лимонад	140.00	t
10	3	Кофе	130.00	t
11	4	Чизкейк	250.00	t
12	4	Мороженое	160.00	t
\.


--
-- Data for Name: order_items; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.order_items (id, order_id, dish_id, quantity, unit_price, discount_percent) FROM stdin;
1	1	1	2	390.00	0.00
2	1	5	2	150.00	0.00
3	2	2	1	320.00	0.00
4	2	8	3	120.00	0.00
5	3	3	1	470.00	0.00
6	3	11	1	250.00	0.00
7	4	4	2	350.00	0.00
8	4	9	2	140.00	0.00
9	5	1	3	390.00	0.00
10	5	7	2	220.00	0.00
11	6	2	2	320.00	0.00
12	6	11	2	250.00	0.00
13	7	3	1	470.00	0.00
14	7	12	1	160.00	0.00
15	8	1	1	390.00	0.00
16	8	8	2	120.00	0.00
\.


--
-- Data for Name: orders; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.orders (id, table_id, status, created_at, placed_at) FROM stdin;
1	1	SERVED	2026-05-05 12:30:00	2026-05-05 12:30:00
2	2	SERVED	2026-05-12 18:30:00	2026-05-12 18:30:00
3	3	SERVED	2026-05-20 13:15:00	2026-05-20 13:15:00
4	5	SERVED	2026-05-25 14:00:00	2026-05-25 14:00:00
5	1	SERVED	2026-06-03 19:20:00	2026-06-03 19:20:00
6	2	SERVED	2026-06-10 12:40:00	2026-06-10 12:40:00
7	4	SERVED	2026-06-15 20:00:00	2026-06-15 20:00:00
8	6	SERVED	2026-06-18 13:30:00	2026-06-18 13:30:00
9	1	COMPOSING	2026-06-23 23:19:00.415042	\N
\.


--
-- Data for Name: promotion_dishes; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.promotion_dishes (promotion_id, dish_id) FROM stdin;
1	12
1	11
\.


--
-- Data for Name: promotions; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.promotions (id, name, discount_percent, start_date, end_date, is_active) FROM stdin;
1	Сладкий июнь	15.00	2026-06-01	2026-06-30	t
\.


--
-- Data for Name: receipts; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.receipts (id, bill_id, total, payment_method, paid_at) FROM stdin;
1	1	1080.00	CASH	2026-05-05 12:30:00
2	2	680.00	CASH	2026-05-12 18:30:00
3	3	720.00	CASH	2026-05-20 13:15:00
4	4	980.00	CASH	2026-05-25 14:00:00
5	5	1610.00	CASH	2026-06-03 19:20:00
6	6	1140.00	CASH	2026-06-10 12:40:00
7	7	630.00	CASH	2026-06-15 20:00:00
8	8	630.00	CASH	2026-06-18 13:30:00
\.


--
-- Data for Name: reservation_tables; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.reservation_tables (reservation_id, table_id) FROM stdin;
1	1
2	2
3	3
4	1
5	2
6	4
7	5
7	6
\.


--
-- Data for Name: reservations; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.reservations (id, guest_name, guest_phone, guests_count, reserve_date, start_time, end_time, status, created_at) FROM stdin;
1	Сидоров Сидор	+70000000005	3	2026-05-05	12:00:00	14:00:00	COMPLETED	2026-06-23 23:19:00.390809
2	Сидоров Сидор	+70000000005	4	2026-05-12	18:00:00	20:00:00	COMPLETED	2026-06-23 23:19:00.390809
3	Кузнецова Анна	+70000000010	2	2026-05-20	13:00:00	15:00:00	COMPLETED	2026-06-23 23:19:00.390809
4	Сидоров Сидор	+70000000005	4	2026-06-03	19:00:00	21:00:00	COMPLETED	2026-06-23 23:19:00.390809
5	Морозов Олег	+70000000011	3	2026-06-10	12:00:00	14:00:00	COMPLETED	2026-06-23 23:19:00.390809
6	Сидоров Сидор	+70000000005	2	2026-06-22	13:00:00	15:00:00	ACTIVE	2026-06-23 23:19:00.390809
7	Сидоров Сидор	+70000000005	4	2026-06-22	19:00:00	21:00:00	ACTIVE	2026-06-23 23:19:00.390809
\.


--
-- Data for Name: restaurant_tables; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.restaurant_tables (id, number, seats, pos_x, pos_y, status) FROM stdin;
1	1	4	40	40	FREE
2	2	4	180	40	FREE
3	3	2	320	40	FREE
4	4	4	460	40	FREE
5	5	4	40	200	FREE
6	6	4	180	200	FREE
7	7	2	320	200	FREE
8	8	4	460	200	FREE
9	9	4	40	360	FREE
10	10	4	180	360	FREE
\.


--
-- Data for Name: roles; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.roles (id, code, name) FROM stdin;
1	ADMIN	Администратор
2	WAITER	Официант
3	KITCHEN	Кухня
4	CLIENT	Клиент
\.


--
-- Data for Name: shift_tables; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.shift_tables (shift_id, table_id) FROM stdin;
1	1
2	2
3	3
4	5
5	1
6	2
7	4
8	6
9	1
9	2
9	3
9	4
9	5
\.


--
-- Data for Name: shifts; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.shifts (id, waiter_id, work_date, status, opened_at, closed_at) FROM stdin;
1	2	2026-05-05	CLOSED	2026-05-05 09:00:00	2026-05-05 23:00:00
2	3	2026-05-12	CLOSED	2026-05-12 09:00:00	2026-05-12 23:00:00
3	2	2026-05-20	CLOSED	2026-05-20 09:00:00	2026-05-20 23:00:00
4	3	2026-05-25	CLOSED	2026-05-25 09:00:00	2026-05-25 23:00:00
5	2	2026-06-03	CLOSED	2026-06-03 09:00:00	2026-06-03 23:00:00
6	3	2026-06-10	CLOSED	2026-06-10 09:00:00	2026-06-10 23:00:00
7	2	2026-06-15	CLOSED	2026-06-15 09:00:00	2026-06-15 23:00:00
8	3	2026-06-18	CLOSED	2026-06-18 09:00:00	2026-06-18 23:00:00
9	2	2026-06-23	OPEN	2026-06-23 23:19:00.41432	\N
\.


--
-- Data for Name: stock; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.stock (dish_id, quantity) FROM stdin;
6	100
10	100
5	98
4	98
9	98
7	98
2	97
11	97
3	98
12	99
1	94
8	95
\.


--
-- Data for Name: stock_movements; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.stock_movements (id, dish_id, change, reason, created_at) FROM stdin;
\.


--
-- Data for Name: users; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.users (id, login, password_hash, role_id, last_name, first_name, middle_name, phone, is_active, created_at) FROM stdin;
1	admin	PBKDF2$100000$tJtVRxWmfY3cV4BD08BjnQ==$ZfaPYj0DNlur+oc1WqwtZFVM5N+uO/TKtbeflRS0EEg=	1	Администраторов	Админ	Админович	+70000000001	t	2026-06-23 23:19:00.381652
2	ivanov	PBKDF2$100000$1dFf3f8nw0uNZA6am+bjmw==$HSbvTCcviokigQQwo9fW/OMIw4rXVm9FwLHxskzzsvU=	2	Иванов	Иван	Иванович	+70000000002	t	2026-06-23 23:19:00.381652
3	petrov	PBKDF2$100000$8S7B6R5+tf1oPaMSGQVJVQ==$BhCO8mnuxOwPxbC0QBvYCyBexmm0ltkZFM1bHD66BoQ=	2	Петров	Пётр	Петрович	+70000000003	t	2026-06-23 23:19:00.381652
4	kitchen	PBKDF2$100000$TtM5fV5X87h0MZ9j5uKx4A==$Zw7LpLvaNk7Z73gBtsV3ty9T6IuWhD+hv8yzRh1iKBc=	3	Кухнин	Повар	Поварович	+70000000004	t	2026-06-23 23:19:00.381652
5	client	PBKDF2$100000$KItrPa/+5n0wD22j17044Q==$VDgBPT81muOY8g4vEpYvWGOVu52qXxUvIJ9Izx8rT94=	4	Сидоров	Сидор	Сидорович	+70000000005	t	2026-06-23 23:19:00.381652
\.


--
-- Name: bills_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.bills_id_seq', 8, true);


--
-- Name: categories_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.categories_id_seq', 4, true);


--
-- Name: dishes_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.dishes_id_seq', 12, true);


--
-- Name: order_items_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.order_items_id_seq', 16, true);


--
-- Name: orders_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.orders_id_seq', 9, true);


--
-- Name: promotions_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.promotions_id_seq', 1, true);


--
-- Name: receipts_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.receipts_id_seq', 8, true);


--
-- Name: reservations_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.reservations_id_seq', 7, true);


--
-- Name: restaurant_tables_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.restaurant_tables_id_seq', 10, true);


--
-- Name: shifts_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.shifts_id_seq', 9, true);


--
-- Name: stock_movements_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.stock_movements_id_seq', 1, false);


--
-- Name: users_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.users_id_seq', 5, true);


--
-- Name: bill_orders bill_orders_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.bill_orders
    ADD CONSTRAINT bill_orders_pkey PRIMARY KEY (bill_id, order_id);


--
-- Name: bills bills_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.bills
    ADD CONSTRAINT bills_pkey PRIMARY KEY (id);


--
-- Name: categories categories_name_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.categories
    ADD CONSTRAINT categories_name_key UNIQUE (name);


--
-- Name: categories categories_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.categories
    ADD CONSTRAINT categories_pkey PRIMARY KEY (id);


--
-- Name: dishes dishes_category_id_name_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.dishes
    ADD CONSTRAINT dishes_category_id_name_key UNIQUE (category_id, name);


--
-- Name: dishes dishes_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.dishes
    ADD CONSTRAINT dishes_pkey PRIMARY KEY (id);


--
-- Name: order_items order_items_order_id_dish_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.order_items
    ADD CONSTRAINT order_items_order_id_dish_id_key UNIQUE (order_id, dish_id);


--
-- Name: order_items order_items_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.order_items
    ADD CONSTRAINT order_items_pkey PRIMARY KEY (id);


--
-- Name: orders orders_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.orders
    ADD CONSTRAINT orders_pkey PRIMARY KEY (id);


--
-- Name: promotion_dishes promotion_dishes_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.promotion_dishes
    ADD CONSTRAINT promotion_dishes_pkey PRIMARY KEY (promotion_id, dish_id);


--
-- Name: promotions promotions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.promotions
    ADD CONSTRAINT promotions_pkey PRIMARY KEY (id);


--
-- Name: receipts receipts_bill_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.receipts
    ADD CONSTRAINT receipts_bill_id_key UNIQUE (bill_id);


--
-- Name: receipts receipts_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.receipts
    ADD CONSTRAINT receipts_pkey PRIMARY KEY (id);


--
-- Name: reservation_tables reservation_tables_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.reservation_tables
    ADD CONSTRAINT reservation_tables_pkey PRIMARY KEY (reservation_id, table_id);


--
-- Name: reservations reservations_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.reservations
    ADD CONSTRAINT reservations_pkey PRIMARY KEY (id);


--
-- Name: restaurant_tables restaurant_tables_number_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.restaurant_tables
    ADD CONSTRAINT restaurant_tables_number_key UNIQUE (number);


--
-- Name: restaurant_tables restaurant_tables_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.restaurant_tables
    ADD CONSTRAINT restaurant_tables_pkey PRIMARY KEY (id);


--
-- Name: roles roles_code_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.roles
    ADD CONSTRAINT roles_code_key UNIQUE (code);


--
-- Name: roles roles_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.roles
    ADD CONSTRAINT roles_pkey PRIMARY KEY (id);


--
-- Name: shift_tables shift_tables_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.shift_tables
    ADD CONSTRAINT shift_tables_pkey PRIMARY KEY (shift_id, table_id);


--
-- Name: shifts shifts_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.shifts
    ADD CONSTRAINT shifts_pkey PRIMARY KEY (id);


--
-- Name: shifts shifts_waiter_id_work_date_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.shifts
    ADD CONSTRAINT shifts_waiter_id_work_date_key UNIQUE (waiter_id, work_date);


--
-- Name: stock_movements stock_movements_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.stock_movements
    ADD CONSTRAINT stock_movements_pkey PRIMARY KEY (id);


--
-- Name: stock stock_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.stock
    ADD CONSTRAINT stock_pkey PRIMARY KEY (dish_id);


--
-- Name: users users_login_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_login_key UNIQUE (login);


--
-- Name: users users_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_pkey PRIMARY KEY (id);


--
-- Name: idx_items_dish; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_items_dish ON public.order_items USING btree (dish_id);


--
-- Name: idx_orders_status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_orders_status ON public.orders USING btree (status);


--
-- Name: idx_orders_table; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_orders_table ON public.orders USING btree (table_id);


--
-- Name: idx_receipts_paid; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_receipts_paid ON public.receipts USING btree (paid_at);


--
-- Name: idx_resv_date; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_resv_date ON public.reservations USING btree (reserve_date);


--
-- Name: idx_shifts_date; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_shifts_date ON public.shifts USING btree (work_date);


--
-- Name: idx_users_role; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_users_role ON public.users USING btree (role_id);


--
-- Name: bill_orders bill_orders_bill_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.bill_orders
    ADD CONSTRAINT bill_orders_bill_id_fkey FOREIGN KEY (bill_id) REFERENCES public.bills(id) ON DELETE CASCADE;


--
-- Name: bill_orders bill_orders_order_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.bill_orders
    ADD CONSTRAINT bill_orders_order_id_fkey FOREIGN KEY (order_id) REFERENCES public.orders(id);


--
-- Name: dishes dishes_category_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.dishes
    ADD CONSTRAINT dishes_category_id_fkey FOREIGN KEY (category_id) REFERENCES public.categories(id);


--
-- Name: order_items order_items_dish_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.order_items
    ADD CONSTRAINT order_items_dish_id_fkey FOREIGN KEY (dish_id) REFERENCES public.dishes(id);


--
-- Name: order_items order_items_order_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.order_items
    ADD CONSTRAINT order_items_order_id_fkey FOREIGN KEY (order_id) REFERENCES public.orders(id) ON DELETE CASCADE;


--
-- Name: orders orders_table_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.orders
    ADD CONSTRAINT orders_table_id_fkey FOREIGN KEY (table_id) REFERENCES public.restaurant_tables(id);


--
-- Name: promotion_dishes promotion_dishes_dish_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.promotion_dishes
    ADD CONSTRAINT promotion_dishes_dish_id_fkey FOREIGN KEY (dish_id) REFERENCES public.dishes(id);


--
-- Name: promotion_dishes promotion_dishes_promotion_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.promotion_dishes
    ADD CONSTRAINT promotion_dishes_promotion_id_fkey FOREIGN KEY (promotion_id) REFERENCES public.promotions(id) ON DELETE CASCADE;


--
-- Name: receipts receipts_bill_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.receipts
    ADD CONSTRAINT receipts_bill_id_fkey FOREIGN KEY (bill_id) REFERENCES public.bills(id);


--
-- Name: reservation_tables reservation_tables_reservation_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.reservation_tables
    ADD CONSTRAINT reservation_tables_reservation_id_fkey FOREIGN KEY (reservation_id) REFERENCES public.reservations(id) ON DELETE CASCADE;


--
-- Name: reservation_tables reservation_tables_table_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.reservation_tables
    ADD CONSTRAINT reservation_tables_table_id_fkey FOREIGN KEY (table_id) REFERENCES public.restaurant_tables(id);


--
-- Name: shift_tables shift_tables_shift_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.shift_tables
    ADD CONSTRAINT shift_tables_shift_id_fkey FOREIGN KEY (shift_id) REFERENCES public.shifts(id) ON DELETE CASCADE;


--
-- Name: shift_tables shift_tables_table_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.shift_tables
    ADD CONSTRAINT shift_tables_table_id_fkey FOREIGN KEY (table_id) REFERENCES public.restaurant_tables(id);


--
-- Name: shifts shifts_waiter_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.shifts
    ADD CONSTRAINT shifts_waiter_id_fkey FOREIGN KEY (waiter_id) REFERENCES public.users(id);


--
-- Name: stock stock_dish_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.stock
    ADD CONSTRAINT stock_dish_id_fkey FOREIGN KEY (dish_id) REFERENCES public.dishes(id);


--
-- Name: stock_movements stock_movements_dish_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.stock_movements
    ADD CONSTRAINT stock_movements_dish_id_fkey FOREIGN KEY (dish_id) REFERENCES public.dishes(id);


--
-- Name: users users_role_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_role_id_fkey FOREIGN KEY (role_id) REFERENCES public.roles(id);


--
-- PostgreSQL database dump complete
--

\unrestrict vN0ae3BdV7BA88hFIDO2VHkC0NKDSLtNiYqLzBevgNV5qUAufKOKLdXyQYhqeqM


# -*- coding: utf-8 -*-
"""Генерация диаграмм для проектной документации средствами Pillow.

Формирует три изображения:
  - usecase.png  — UML-диаграмма вариантов использования;
  - flowchart.png — блок-схема процесса оформления заказа;
  - er.png       — ER-диаграмма базы данных.
"""
import os
from PIL import Image, ImageDraw, ImageFont

OUT = os.path.join(os.path.dirname(__file__), "..", "diagrams")
os.makedirs(OUT, exist_ok=True)

FONT_PATH = r"C:\Windows\Fonts\arial.ttf"
FONT_BOLD = r"C:\Windows\Fonts\arialbd.ttf"


def font(size, bold=False):
    return ImageFont.truetype(FONT_BOLD if bold else FONT_PATH, size)


def text_center(d, box, s, f, fill="black"):
    x0, y0, x1, y1 = box
    tb = d.textbbox((0, 0), s, font=f)
    tw, th = tb[2] - tb[0], tb[3] - tb[1]
    d.text((x0 + (x1 - x0 - tw) / 2, y0 + (y1 - y0 - th) / 2 - tb[1]), s, font=f, fill=fill)


def box(d, xy, s, f, fill="#eef3fb", outline="#2b5797", width=2, radius=10):
    d.rounded_rectangle(xy, radius=radius, fill=fill, outline=outline, width=width)
    text_center(d, xy, s, f)


def multiline_box(d, xy, title, lines, ft, fl, fill="#ffffff", outline="#2b5797"):
    x0, y0, x1, y1 = xy
    d.rectangle(xy, fill=fill, outline=outline, width=2)
    d.rectangle((x0, y0, x1, y0 + 26), fill="#2b5797", outline="#2b5797")
    text_center(d, (x0, y0, x1, y0 + 26), title, ft, fill="white")
    ty = y0 + 32
    for ln in lines:
        d.text((x0 + 8, ty), ln, font=fl, fill="black")
        ty += 18


def arrow(d, p0, p1, color="#444", width=2):
    d.line([p0, p1], fill=color, width=width)
    import math
    ang = math.atan2(p1[1] - p0[1], p1[0] - p0[0])
    L = 10
    for da in (math.radians(25), -math.radians(25)):
        d.line([p1, (p1[0] - L * math.cos(ang + da), p1[1] - L * math.sin(ang + da))], fill=color, width=width)


# --------------------------------------------------------------------------
# 1. UML Use-Case
# --------------------------------------------------------------------------
def build_usecase():
    W, H = 1400, 900
    img = Image.new("RGB", (W, H), "white")
    d = ImageDraw.Draw(img)
    ft = font(20, True); fa = font(15, True); fu = font(14)

    text_center(d, (0, 10, W, 45), "Диаграмма вариантов использования системы «Прайм Бургер»", ft)

    actors = [("Клиент", 70, 230), ("Официант", 70, 560),
              ("Администратор", W - 130, 230), ("Кухня", W - 130, 600)]
    actor_pos = {}
    for name, x, y in actors:
        # фигурка актёра
        d.ellipse((x - 12, y - 40, x + 12, y - 16), outline="black", width=2)
        d.line([(x, y - 16), (x, y + 15)], fill="black", width=2)
        d.line([(x - 18, y - 5), (x + 18, y - 5)], fill="black", width=2)
        d.line([(x, y + 15), (x - 15, y + 40)], fill="black", width=2)
        d.line([(x, y + 15), (x + 15, y + 40)], fill="black", width=2)
        text_center(d, (x - 60, y + 42, x + 60, y + 64), name, fa)
        actor_pos[name] = (x, y)

    cases = {
        "Регистрация / Вход": (560, 70),
        "Бронирование стола": (560, 150),
        "Отмена брони": (560, 230),
        "Просмотр схемы зала": (560, 310),
        "Открытие/закрытие смены": (560, 400),
        "Ведение заказа": (560, 480),
        "Оформление заказа": (560, 560),
        "Оплата и чек": (560, 640),
        "Изменение статуса (кухня)": (560, 720),
        "Управление меню/складом": (840, 150),
        "Планирование смен": (840, 230),
        "Управление пользователями": (840, 310),
        "Просмотр отчётов": (840, 400),
    }
    cpos = {}
    for name, (x, y) in cases.items():
        w = 230 if x < 800 else 250
        xy = (x - w / 2, y - 26, x + w / 2, y + 26)
        d.ellipse(xy, fill="#eef3fb", outline="#2b5797", width=2)
        text_center(d, xy, name, fu)
        cpos[name] = (x, y, w)

    def link(actor, case):
        ax, ay = actor_pos[actor]
        cx, cy, cw = cpos[case]
        side = cx - cw / 2 if ax < cx else cx + cw / 2
        d.line([(ax + (20 if ax < cx else -20), ay), (side, cy)], fill="#888", width=1)

    for c in ["Регистрация / Вход", "Бронирование стола", "Отмена брони", "Просмотр схемы зала"]:
        link("Клиент", c)
    for c in ["Регистрация / Вход", "Просмотр схемы зала", "Открытие/закрытие смены",
              "Ведение заказа", "Оформление заказа", "Оплата и чек", "Бронирование стола"]:
        link("Официант", c)
    for c in ["Регистрация / Вход", "Управление меню/складом", "Планирование смен",
              "Управление пользователями", "Просмотр отчётов", "Просмотр схемы зала"]:
        link("Администратор", c)
    for c in ["Регистрация / Вход", "Изменение статуса (кухня)"]:
        link("Кухня", c)

    img.save(os.path.join(OUT, "usecase.png"))


# --------------------------------------------------------------------------
# 2. Блок-схема процесса оформления заказа
# --------------------------------------------------------------------------
def build_flowchart():
    W, H = 1020, 1180
    img = Image.new("RGB", (W, H), "white")
    d = ImageDraw.Draw(img)
    ft = font(20, True); f = font(14)
    cx = W // 2

    text_center(d, (0, 10, W, 45), "Блок-схема: оформление заказа с контролем склада", ft)

    def term(y, s):
        d.rounded_rectangle((cx - 110, y, cx + 110, y + 44), radius=22, fill="#dff0d8", outline="#3c763d", width=2)
        text_center(d, (cx - 110, y, cx + 110, y + 44), s, f)

    def proc(y, s, fill="#eef3fb"):
        d.rectangle((cx - 150, y, cx + 150, y + 46), fill=fill, outline="#2b5797", width=2)
        text_center(d, (cx - 150, y, cx + 150, y + 46), s, f)

    def dec(y, s):
        d.polygon([(cx, y), (cx + 175, y + 38), (cx, y + 76), (cx - 175, y + 38)],
                  fill="#fcf8e3", outline="#8a6d3b")
        text_center(d, (cx - 150, y, cx + 150, y + 76), s, f)

    def varrow(y0, y1):
        arrow(d, (cx, y0), (cx, y1))

    y = 60
    term(y, "Начало"); varrow(y + 44, y + 70)
    y = 130; proc(y, "Выбор блюда и количества"); varrow(y + 46, y + 72)
    y = 210; dec(y, "Достаточно порций\nна складе?")
    # нет -> вправо
    d.line([(cx + 175, y + 38), (cx + 300, y + 38)], fill="#444", width=2)
    d.text((cx + 185, y + 16), "Нет", font=f, fill="#a94442")
    d.rectangle((cx + 300, y + 14, W - 10, y + 62), fill="#f2dede", outline="#a94442", width=2)
    text_center(d, (cx + 300, y + 14, W - 10, y + 62), "Доступно X порций", f)
    arrow(d, (W - 90, y + 14), (W - 90, 152)); d.line([(W - 90, 152), (cx + 150, 152)], fill="#444", width=2)
    # да -> вниз
    d.text((cx + 10, y + 80), "Да", font=f, fill="#3c763d")
    varrow(y + 76, y + 110)
    y = 320; proc(y, "Списание порций со склада"); varrow(y + 46, y + 72)
    y = 400; proc(y, "Добавление позиции в заказ"); varrow(y + 46, y + 72)
    y = 480; proc(y, "Сообщение об успешном добавлении"); varrow(y + 46, y + 72)
    y = 560; dec(y, "Добавить ещё\nблюдо?")
    d.line([(cx - 175, y + 38), (cx - 320, y + 38)], fill="#444", width=2)
    d.text((cx - 230, y + 16), "Да", font=f, fill="#3c763d")
    d.line([(cx - 320, y + 38), (cx - 320, 153)], fill="#444", width=2)
    arrow(d, (cx - 320, 153), (cx - 150, 153))
    d.text((cx + 10, y + 80), "Нет", font=f, fill="#a94442")
    varrow(y + 76, y + 110)
    y = 670; proc(y, "Оформление заказа\n(фиксация даты и времени)"); varrow(y + 46, y + 72)
    y = 760; proc(y, "Передача заказа на кухню"); varrow(y + 46, y + 72)
    y = 840; proc(y, "Готовка → Готов → Выдача"); varrow(y + 46, y + 72)
    y = 920; proc(y, "Формирование счёта и оплата"); varrow(y + 46, y + 72)
    y = 1000; proc(y, "Формирование чека"); varrow(y + 46, y + 72)
    y = 1080; term(y, "Конец")

    img.save(os.path.join(OUT, "flowchart.png"))


# --------------------------------------------------------------------------
# 3. ER-диаграмма
# --------------------------------------------------------------------------
def build_er():
    W, H = 1500, 1000
    img = Image.new("RGB", (W, H), "white")
    d = ImageDraw.Draw(img)
    ft = font(20, True); fttl = font(13, True); fl = font(12)

    text_center(d, (0, 8, W, 40), "ER-диаграмма базы данных «Прайм Бургер»", ft)

    ent = {
        "roles":         (40, 60, ["PK id", "code", "name"]),
        "users":         (40, 200, ["PK id", "FK role_id", "login", "password_hash", "ФИО, phone"]),
        "shifts":        (40, 420, ["PK id", "FK waiter_id", "work_date", "status"]),
        "shift_tables":  (40, 620, ["PK,FK shift_id", "PK,FK table_id"]),
        "restaurant_tables": (380, 560, ["PK id", "number", "seats", "pos_x, pos_y", "status"]),
        "reservations":  (380, 60, ["PK id", "FK client_id", "guests_count", "reserve_date", "start/end_time", "status"]),
        "reservation_tables": (380, 320, ["PK,FK reservation_id", "PK,FK table_id"]),
        "categories":    (760, 60, ["PK id", "name"]),
        "dishes":        (760, 200, ["PK id", "FK category_id", "name", "base_price", "is_active"]),
        "stock":         (760, 420, ["PK,FK dish_id", "quantity"]),
        "promotions":    (760, 580, ["PK id", "discount_percent", "start/end_date"]),
        "orders":        (1120, 200, ["PK id", "FK reservation_id", "FK table_id", "FK waiter_id", "status", "placed_at"]),
        "order_items":   (1120, 60, ["PK id", "FK order_id", "FK dish_id", "quantity", "unit_price", "discount"]),
        "bills":         (1120, 480, ["PK id", "FK table_id", "status", "total_amount"]),
        "receipts":      (1120, 700, ["PK id", "FK bill_id", "FK waiter_id", "total", "paid_at"]),
        "bill_orders":   (820, 760, ["PK,FK bill_id", "PK,FK order_id"]),
    }
    pos = {}
    for name, (x, y, lines) in ent.items():
        w, h = 230, 30 + 18 * len(lines)
        multiline_box(d, (x, y, x + w, y + h), name, lines, fttl, fl)
        pos[name] = (x, y, w, h)

    def rel(a, b):
        ax, ay, aw, ah = pos[a]; bx, by, bw, bh = pos[b]
        d.line([(ax + aw / 2, ay + ah / 2), (bx + bw / 2, by + bh / 2)], fill="#c0504d", width=2)

    for a, b in [("users", "roles"), ("shifts", "users"), ("shift_tables", "shifts"),
                 ("shift_tables", "restaurant_tables"), ("reservations", "users"),
                 ("reservation_tables", "reservations"), ("reservation_tables", "restaurant_tables"),
                 ("dishes", "categories"), ("stock", "dishes"), ("order_items", "orders"),
                 ("order_items", "dishes"), ("orders", "reservations"), ("orders", "restaurant_tables"),
                 ("orders", "users"), ("bills", "restaurant_tables"), ("receipts", "bills"),
                 ("receipts", "users"), ("bill_orders", "bills"), ("bill_orders", "orders")]:
        rel(a, b)

    # перерисовать сущности поверх линий
    for name, (x, y, lines) in ent.items():
        w, h = 230, 30 + 18 * len(lines)
        multiline_box(d, (x, y, x + w, y + h), name, lines, fttl, fl)

    img.save(os.path.join(OUT, "er.png"))


if __name__ == "__main__":
    build_usecase()
    build_flowchart()
    build_er()
    print("Диаграммы сохранены в", os.path.abspath(OUT))

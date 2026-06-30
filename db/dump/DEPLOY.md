# Развёртывание БД и приложения на другом компьютере

## Файл дампа
`restaurant_db.sql` — полный самодостаточный дамп (`pg_dump --create --clean`):
содержит создание базы, типы, таблицы, индексы, представления, функции и все данные.
Снят с PostgreSQL 18.

## 1. Установить PostgreSQL
PostgreSQL **17 или 18** (дамп снят на 18; на 17 разворачивается без проблем).
На версии ниже 17 лучше разворачивать не дампом, а скриптами из каталога `db/`
(`01_schema.sql` → `02_views_functions.sql` → `03_seed.sql`) — они переносимы на 14+.

## 2. Восстановить базу из дампа
Подключаемся к служебной базе `postgres` — дамп сам пересоздаёт `restaurant_db`
(DROP DATABASE IF EXISTS + CREATE DATABASE):

```bash
# Linux/macOS
PGPASSWORD=ВАШ_ПАРОЛЬ psql -U postgres -h localhost -p ПОРТ -d postgres -f restaurant_db.sql
```

```powershell
# Windows (PowerShell)
$env:PGPASSWORD="ВАШ_ПАРОЛЬ"
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -h localhost -p ПОРТ -d postgres -f restaurant_db.sql
```

`ПОРТ` — порт PostgreSQL на новом ПК (обычно стандартный **5432**).

## 3. Указать приложению строку подключения
Приложение читает строку подключения из переменной окружения `RESTAURANT_DB`,
а если её нет — берёт значение по умолчанию из кода
([src/Restaurant.Data/Db.cs](../../src/Restaurant.Data/Db.cs), поле `DefaultConnectionString`).

**Способ А (рекомендуется) — переменная окружения, код не трогаем:**

```powershell
# Windows
setx RESTAURANT_DB "Host=localhost;Port=5432;Database=restaurant_db;Username=postgres;Password=ВАШ_ПАРОЛЬ"
```
```bash
# Linux/macOS
export RESTAURANT_DB="Host=localhost;Port=5432;Database=restaurant_db;Username=postgres;Password=ВАШ_ПАРОЛЬ"
```

**Способ Б — поправить код.** В файле `src/Restaurant.Data/Db.cs` изменить строку:
```csharp
private const string DefaultConnectionString =
    "Host=localhost;Port=5432;Database=restaurant_db;Username=postgres;Password=ВАШ_ПАРОЛЬ";
```
Поменять под новый ПК нужно: **Port** (5433 → 5432, если там стандартный порт)
и **Password** (на пароль postgres нового ПК). Host/Database обычно те же.

## 4. Запустить приложение
Нужен **.NET SDK 10** (или новее) и Windows (интерфейс на Windows Forms).
```bash
dotnet build RestaurantApp.slnx
dotnet run --project src/Restaurant.UI
```

## Тестовые учётные записи
| Роль | Логин | Пароль |
|------|-------|--------|
| Администратор | admin | admin123 |
| Официант | ivanov / petrov | waiter123 |
| Кухня | kitchen | kitchen123 |
| Клиент | client | client123 |

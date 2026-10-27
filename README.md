# CinemaAdoNet — Система продажи билетов в кинотеатр

Консольное приложение на C# (.NET 8) для работы с учебной базой данных кинотеатра `cinema_training` в PostgreSQL с использованием ADO.NET (`Npgsql`).

## Стек технологий
- **Язык и платформа:** C#, .NET 8
- **Доступ к данным:** ADO.NET (`NpgsqlConnection`, `NpgsqlCommand`, `NpgsqlDataReader`, `NpgsqlTransaction`)
- **СУБД:** PostgreSQL (`Npgsql` 8.x)

## Подготовка и запуск

1. Создайте пустую базу данных `cinema_training` в PostgreSQL и выполните скрипт `cinema_training.sql`:
   ```bash
   psql -U postgres -c "CREATE DATABASE cinema_training;"
   psql -U postgres -d cinema_training -f cinema_training.sql
   ```

2. Укажите пароль к PostgreSQL через переменную окружения `CINEMA_DB_PASSWORD`:

   **PowerShell (Windows):**
   ```powershell
   $env:CINEMA_DB_PASSWORD="ваш_пароль"
   dotnet run
   ```

   **Bash (Linux / macOS):**
   ```bash
   export CINEMA_DB_PASSWORD="ваш_пароль"
   dotnet run
   ```

   Либо задайте полную строку подключения через переменную окружения `CINEMA_DB_CONNECTION`:
   ```text
   Host=localhost;Port=5432;Database=cinema_training;Username=postgres;Password=ВАШ_ПАРОЛЬ
   ```

## Реализованный функционал
- **Задание 1:** Проверка подключения к БД при запуске (`SELECT CURRENT_TIMESTAMP`).
- **Задание 2:** Просмотр всех фильмов и поиск фильма по части названия (`ILIKE` с параметром).
- **Задание 3:** Расписание сеансов с подсчётом общего и свободного числа мест (`JOIN`, `GROUP BY`) и фильтрацией по дате или диапазону дат.
- **Задание 4:** Поиск клиента по email и вывод истории его билетов.
- **Задание 5:** Регистрация нового клиента с проверкой обязательных полей, возвратом `RETURNING customer_id` и обработкой дубликата email.
- **Задание 6:** Покупка билета в транзакции (`BeginTransaction`, `Commit`, `Rollback`) с защитой от одновременной покупки одного места (`FOR UPDATE` и перехват `UniqueViolation`).
- **Задание 7:** Отмена действующего билета (`UPDATE ... SET status = 'Cancelled'`) с проверкой `ExecuteNonQuery()`.
- **Задание 8:** Отчёт по сеансам (`COUNT`, `SUM` с типом `decimal`) и топ-3 фильма по количеству проданных билетов.

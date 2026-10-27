using System;
using System.Globalization;
using Npgsql;
using NpgsqlTypes;

namespace CinemaAdoNet;

internal static class Program
{
    private static readonly string ConnectionString = BuildConnectionString();

    private static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine("=== Система продажи билетов в кинотеатр (ADO.NET + PostgreSQL) ===");

        if (!TestConnection())
        {
            Console.WriteLine("Приложение остановлено из-за ошибки подключения к базе данных.");
            return;
        }

        RunMainMenu();
    }

    private static string BuildConnectionString()
    {
        string? envConnection = Environment.GetEnvironmentVariable("CINEMA_DB_CONNECTION");
        if (!string.IsNullOrWhiteSpace(envConnection))
        {
            return envConnection;
        }

        string password = Environment.GetEnvironmentVariable("CINEMA_DB_PASSWORD") ?? "ВАШ_ПАРОЛЬ";
        return $"Host=localhost;Port=5432;Database=cinema_training;Username=postgres;Password={password}";
    }

    private static bool TestConnection()
    {
        try
        {
            using var connection = new NpgsqlConnection(ConnectionString);
            connection.Open();

            const string sql = "SELECT CURRENT_TIMESTAMP;";
            using var cmd = new NpgsqlCommand(sql, connection);
            object? serverTime = cmd.ExecuteScalar();

            Console.WriteLine($"[ОК] Успешное подключение к базе данных 'cinema_training'. Время сервера БД: {serverTime:yyyy-MM-dd HH:mm:ss}");
            return true;
        }
        catch (PostgresException pgEx)
        {
            PrintDatabaseError("Не удалось подключиться к базе данных PostgreSQL.", pgEx);
            return false;
        }
        catch (Exception ex)
        {
            PrintGeneralError("Ошибка при проверке подключения к базе данных. Проверьте строку подключения и доступность сервера.", ex);
            return false;
        }
    }

    private static void RunMainMenu()
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("---------------- МЕНЮ ----------------");
            Console.WriteLine("1. Проверить подключение к БД (Задание 1)");
            Console.WriteLine("2. Список всех фильмов (Задание 2)");
            Console.WriteLine("3. Поиск фильма по части названия (Задание 2)");
            Console.WriteLine("4. Расписание сеансов и свободные места (Задание 3)");
            Console.WriteLine("5. Фильтр сеансов по дате или диапазону дат (Задание 3)");
            Console.WriteLine("6. Поиск клиента и его билетов по Email (Задание 4)");
            Console.WriteLine("7. Регистрация нового клиента (Задание 5)");
            Console.WriteLine("8. Покупка билета (Задание 6)");
            Console.WriteLine("9. Отмена билета (Задание 7)");
            Console.WriteLine("10. Отчёт по сеансам и топ-3 фильма (Задание 8)");
            Console.WriteLine("0. Выход");
            Console.Write("Выберите пункт меню: ");

            string? choice = Console.ReadLine()?.Trim();
            Console.WriteLine();

            switch (choice)
            {
                case "1":
                    TestConnection();
                    break;
                case "2":
                    ShowFilms(null);
                    break;
                case "3":
                    SearchFilmsByTitle();
                    break;
                case "4":
                    ShowScreenings(null, null);
                    break;
                case "5":
                    FilterScreeningsByDate();
                    break;
                case "6":
                    FindCustomerAndTickets();
                    break;
                case "7":
                    RegisterCustomer();
                    break;
                case "8":
                    PurchaseTicket();
                    break;
                case "9":
                    CancelTicket();
                    break;
                case "10":
                    ShowScreeningsReportAndTopFilms();
                    break;
                case "0":
                    Console.WriteLine("Завершение работы приложения.");
                    return;
                default:
                    Console.WriteLine("Неизвестная команда. Введите номер пункта от 0 до 10.");
                    break;
            }
        }
    }

    private static void SearchFilmsByTitle()
    {
        Console.Write("Введите часть названия фильма для поиска: ");
        string? searchPart = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(searchPart))
        {
            Console.WriteLine("Строка поиска не может быть пустой.");
            return;
        }

        ShowFilms(searchPart);
    }

    private static void ShowFilms(string? titleFilter)
    {
        const string sqlAll = @"
            SELECT film_id, title, genre, duration_minutes, age_rating, release_year
            FROM films
            ORDER BY film_id;";

        const string sqlFiltered = @"
            SELECT film_id, title, genre, duration_minutes, age_rating, release_year
            FROM films
            WHERE title ILIKE @pattern
            ORDER BY film_id;";

        try
        {
            using var connection = new NpgsqlConnection(ConnectionString);
            connection.Open();

            bool hasFilter = !string.IsNullOrWhiteSpace(titleFilter);
            using var cmd = new NpgsqlCommand(hasFilter ? sqlFiltered : sqlAll, connection);

            if (hasFilter)
            {
                cmd.Parameters.Add("@pattern", NpgsqlDbType.Varchar).Value = $"%{titleFilter}%";
            }

            using NpgsqlDataReader reader = cmd.ExecuteReader();

            Console.WriteLine($"{"ID",-4} | {"Название",-28} | {"Жанр",-14} | {"Длит.",-8} | {"Рейтинг",-7} | {"Год",-4}");
            Console.WriteLine(new string('-', 80));

            int count = 0;
            while (reader.Read())
            {
                int filmId = reader.GetInt32(0);
                string title = reader.GetString(1);
                string genre = reader.GetString(2);
                int duration = reader.GetInt32(3);
                int ageRating = reader.GetInt32(4);
                int releaseYear = reader.GetInt32(5);

                Console.WriteLine($"{filmId,-4} | {title,-28} | {genre,-14} | {duration + " мин",-8} | {ageRating + "+",-7} | {releaseYear,-4}");
                count++;
            }

            if (count == 0)
            {
                Console.WriteLine("Фильмы по заданному условию не найдены.");
            }
        }
        catch (PostgresException pgEx)
        {
            PrintDatabaseError("Ошибка БД при получении списка фильмов.", pgEx);
        }
        catch (Exception ex)
        {
            PrintGeneralError("Не удалось загрузить список фильмов.", ex);
        }
    }

    private static void FilterScreeningsByDate()
    {
        Console.WriteLine("Выберите режим фильтрации:");
        Console.WriteLine("1 — По одной выбранной дате (ГГГГ-ММ-ДД)");
        Console.WriteLine("2 — По диапазону дат (с ГГГГ-ММ-ДД по ГГГГ-ММ-ДД включительно)");
        Console.Write("Ваш выбор: ");
        string? mode = Console.ReadLine()?.Trim();

        if (mode == "1")
        {
            if (!TryReadDate("Введите дату сеансов (ГГГГ-ММ-ДД): ", out DateTime date))
            {
                return;
            }

            DateTime from = date.Date;
            DateTime toExclusive = date.Date.AddDays(1);
            ShowScreenings(from, toExclusive);
        }
        else if (mode == "2")
        {
            if (!TryReadDate("Введите начальную дату (ГГГГ-ММ-ДД): ", out DateTime startDate))
            {
                return;
            }

            if (!TryReadDate("Введите конечную дату (ГГГГ-ММ-ДД): ", out DateTime endDate))
            {
                return;
            }

            if (endDate.Date < startDate.Date)
            {
                Console.WriteLine("Конечная дата не может быть раньше начальной.");
                return;
            }

            DateTime from = startDate.Date;
            DateTime toExclusive = endDate.Date.AddDays(1);
            ShowScreenings(from, toExclusive);
        }
        else
        {
            Console.WriteLine("Некорректный выбор режима фильтрации.");
        }
    }

    private static void ShowScreenings(DateTime? fromStart, DateTime? toExclusive)
    {
        const string sql = @"
            SELECT
                s.screening_id,
                f.title,
                f.genre,
                s.starts_at,
                h.hall_name,
                s.ticket_price,
                h.seat_count AS total_seats,
                h.seat_count - COUNT(t.ticket_id) AS free_seats
            FROM screenings AS s
            JOIN films AS f ON f.film_id = s.film_id
            JOIN halls AS h ON h.hall_id = s.hall_id
            LEFT JOIN tickets AS t
                ON t.screening_id = s.screening_id
               AND t.status = 'Purchased'
            WHERE (@from_date IS NULL OR s.starts_at >= @from_date)
              AND (@to_date IS NULL OR s.starts_at < @to_date)
            GROUP BY
                s.screening_id,
                f.title,
                f.genre,
                s.starts_at,
                h.hall_name,
                s.ticket_price,
                h.seat_count
            ORDER BY s.starts_at, s.screening_id;";

        try
        {
            using var connection = new NpgsqlConnection(ConnectionString);
            connection.Open();

            using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.Add("@from_date", NpgsqlDbType.Timestamp).Value =
                fromStart.HasValue ? fromStart.Value : DBNull.Value;
            cmd.Parameters.Add("@to_date", NpgsqlDbType.Timestamp).Value =
                toExclusive.HasValue ? toExclusive.Value : DBNull.Value;

            using NpgsqlDataReader reader = cmd.ExecuteReader();

            Console.WriteLine($"{"ID",-4} | {"Фильм",-25} | {"Жанр",-13} | {"Начало сеанса",-16} | {"Зал",-7} | {"Цена",-8} | {"Мест",-5} | {"Своб.",-5}");
            Console.WriteLine(new string('-', 100));

            int count = 0;
            while (reader.Read())
            {
                int screeningId = reader.GetInt32(0);
                string title = reader.GetString(1);
                string genre = reader.GetString(2);
                DateTime startsAt = reader.GetDateTime(3);
                string hallName = reader.GetString(4);
                decimal price = reader.GetDecimal(5);
                int totalSeats = reader.GetInt32(6);
                long freeSeats = reader.GetInt64(7);

                Console.WriteLine(
                    $"{screeningId,-4} | {title,-25} | {genre,-13} | {startsAt:yyyy-MM-dd HH:mm,-16} | {hallName,-7} | {price,8:F2} | {totalSeats,5} | {freeSeats,5}");
                count++;
            }

            if (count == 0)
            {
                Console.WriteLine("Сеансы за указанный период не найдены.");
            }
        }
        catch (PostgresException pgEx)
        {
            PrintDatabaseError("Ошибка БД при получении расписания сеансов.", pgEx);
        }
        catch (Exception ex)
        {
            PrintGeneralError("Не удалось получить расписание сеансов.", ex);
        }
    }

    private static void FindCustomerAndTickets()
    {
        Console.Write("Введите email клиента: ");
        string? email = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            Console.WriteLine("Email не может быть пустым.");
            return;
        }

        const string findCustomerSql = @"
            SELECT customer_id, first_name, last_name, email, phone
            FROM customers
            WHERE LOWER(email) = LOWER(@email);";

        const string findTicketsSql = @"
            SELECT
                t.ticket_id,
                f.title,
                s.starts_at,
                h.hall_name,
                st.seat_number,
                s.ticket_price,
                t.status
            FROM tickets AS t
            JOIN screenings AS s ON s.screening_id = t.screening_id
            JOIN films AS f ON f.film_id = s.film_id
            JOIN halls AS h ON h.hall_id = s.hall_id
            JOIN seats AS st ON st.seat_id = t.seat_id
            WHERE t.customer_id = @customer_id
            ORDER BY s.starts_at DESC, t.ticket_id DESC;";

        try
        {
            using var connection = new NpgsqlConnection(ConnectionString);
            connection.Open();

            int customerId;
            string fullName;
            string customerEmail;
            string phone;

            using (var customerCmd = new NpgsqlCommand(findCustomerSql, connection))
            {
                customerCmd.Parameters.Add("@email", NpgsqlDbType.Varchar).Value = email;

                using NpgsqlDataReader custReader = customerCmd.ExecuteReader();
                if (!custReader.Read())
                {
                    Console.WriteLine($"Клиент с адресом '{email}' не найден.");
                    return;
                }

                customerId = custReader.GetInt32(0);
                fullName = $"{custReader.GetString(1)} {custReader.GetString(2)}";
                customerEmail = custReader.GetString(3);
                phone = custReader.IsDBNull(4) ? "не указан" : custReader.GetString(4);
            }

            Console.WriteLine($"Клиент найден: [{customerId}] {fullName}, Email: {customerEmail}, Телефон: {phone}");
            Console.WriteLine();

            using var ticketsCmd = new NpgsqlCommand(findTicketsSql, connection);
            ticketsCmd.Parameters.Add("@customer_id", NpgsqlDbType.Integer).Value = customerId;

            using NpgsqlDataReader ticketReader = ticketsCmd.ExecuteReader();

            Console.WriteLine($"{"Билет №",-8} | {"Фильм",-25} | {"Время сеанса",-16} | {"Зал",-7} | {"Место",-5} | {"Цена",-8} | {"Статус",-10}");
            Console.WriteLine(new string('-', 92));

            int ticketCount = 0;
            while (ticketReader.Read())
            {
                int ticketId = ticketReader.GetInt32(0);
                string title = ticketReader.GetString(1);
                DateTime startsAt = ticketReader.GetDateTime(2);
                string hallName = ticketReader.GetString(3);
                int seatNumber = ticketReader.GetInt32(4);
                decimal price = ticketReader.GetDecimal(5);
                string status = ticketReader.GetString(6);

                Console.WriteLine(
                    $"{ticketId,-8} | {title,-25} | {startsAt:yyyy-MM-dd HH:mm,-16} | {hallName,-7} | {seatNumber,5} | {price,8:F2} | {status,-10}");
                ticketCount++;
            }

            if (ticketCount == 0)
            {
                Console.WriteLine("У данного клиента пока нет оформленных билетов.");
            }
        }
        catch (PostgresException pgEx)
        {
            PrintDatabaseError("Ошибка БД при поиске клиента и его билетов.", pgEx);
        }
        catch (Exception ex)
        {
            PrintGeneralError("Не удалось выполнить поиск клиента.", ex);
        }
    }

    private static void RegisterCustomer()
    {
        Console.Write("Введите имя клиента (обязательно): ");
        string? firstName = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(firstName))
        {
            Console.WriteLine("Ошибка: имя клиента обязательно для заполнения.");
            return;
        }

        Console.Write("Введите фамилию клиента (обязательно): ");
        string? lastName = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(lastName))
        {
            Console.WriteLine("Ошибка: фамилия клиента обязательна для заполнения.");
            return;
        }

        Console.Write("Введите email клиента (обязательно): ");
        string? email = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            Console.WriteLine("Ошибка: email клиента обязателен для заполнения.");
            return;
        }

        Console.Write("Введите телефон клиента (обязательно): ");
        string? phone = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(phone))
        {
            Console.WriteLine("Ошибка: телефон клиента обязателен для заполнения.");
            return;
        }

        const string insertSql = @"
            INSERT INTO customers (first_name, last_name, email, phone)
            VALUES (@first_name, @last_name, @email, @phone)
            RETURNING customer_id;";

        try
        {
            using var connection = new NpgsqlConnection(ConnectionString);
            connection.Open();

            using var cmd = new NpgsqlCommand(insertSql, connection);
            cmd.Parameters.Add("@first_name", NpgsqlDbType.Varchar).Value = firstName;
            cmd.Parameters.Add("@last_name", NpgsqlDbType.Varchar).Value = lastName;
            cmd.Parameters.Add("@email", NpgsqlDbType.Varchar).Value = email;
            cmd.Parameters.Add("@phone", NpgsqlDbType.Varchar).Value = phone;

            object? result = cmd.ExecuteScalar();
            int newCustomerId = Convert.ToInt32(result, CultureInfo.InvariantCulture);

            Console.WriteLine($"Клиент успешно зарегистрирован! Присвоенный ID клиента: {newCustomerId}");
        }
        catch (PostgresException pgEx) when (pgEx.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            Console.WriteLine($"Клиент с email '{email}' уже зарегистрирован в базе данных.");
        }
        catch (PostgresException pgEx)
        {
            PrintDatabaseError("Ошибка БД при регистрации нового клиента.", pgEx);
        }
        catch (Exception ex)
        {
            PrintGeneralError("Не удалось зарегистрировать клиента.", ex);
        }
    }

    private static void PurchaseTicket()
    {
        if (!TryReadPositiveInt("Введите ID сеанса (screening_id): ", out int screeningId))
        {
            return;
        }

        if (!TryReadPositiveInt("Введите ID клиента (customer_id): ", out int customerId))
        {
            return;
        }

        if (!TryReadPositiveInt("Введите номер места (seat_number): ", out int seatNumber))
        {
            return;
        }

        try
        {
            using var connection = new NpgsqlConnection(ConnectionString);
            connection.Open();

            using NpgsqlTransaction transaction = connection.BeginTransaction();

            try
            {
                const string checkScreeningSql = @"
                    SELECT s.hall_id, f.title, s.starts_at, s.ticket_price, h.hall_name
                    FROM screenings AS s
                    JOIN films AS f ON f.film_id = s.film_id
                    JOIN halls AS h ON h.hall_id = s.hall_id
                    WHERE s.screening_id = @screening_id;";

                int hallId;
                string filmTitle;
                DateTime startsAt;
                decimal ticketPrice;
                string hallName;

                using (var screeningCmd = new NpgsqlCommand(checkScreeningSql, connection, transaction))
                {
                    screeningCmd.Parameters.Add("@screening_id", NpgsqlDbType.Integer).Value = screeningId;
                    using NpgsqlDataReader reader = screeningCmd.ExecuteReader();
                    if (!reader.Read())
                    {
                        Console.WriteLine($"Сеанс с ID {screeningId} не существует.");
                        transaction.Rollback();
                        return;
                    }

                    hallId = reader.GetInt32(0);
                    filmTitle = reader.GetString(1);
                    startsAt = reader.GetDateTime(2);
                    ticketPrice = reader.GetDecimal(3);
                    hallName = reader.GetString(4);
                }

                const string checkCustomerSql = @"
                    SELECT COUNT(1)
                    FROM customers
                    WHERE customer_id = @customer_id;";

                using (var customerCmd = new NpgsqlCommand(checkCustomerSql, connection, transaction))
                {
                    customerCmd.Parameters.Add("@customer_id", NpgsqlDbType.Integer).Value = customerId;
                    long customerExists = Convert.ToInt64(customerCmd.ExecuteScalar(), CultureInfo.InvariantCulture);
                    if (customerExists == 0)
                    {
                        Console.WriteLine($"Клиент с ID {customerId} не существует.");
                        transaction.Rollback();
                        return;
                    }
                }

                const string checkSeatSql = @"
                    SELECT seat_id
                    FROM seats
                    WHERE hall_id = @hall_id AND seat_number = @seat_number
                    FOR UPDATE;";

                int seatId;
                using (var seatCmd = new NpgsqlCommand(checkSeatSql, connection, transaction))
                {
                    seatCmd.Parameters.Add("@hall_id", NpgsqlDbType.Integer).Value = hallId;
                    seatCmd.Parameters.Add("@seat_number", NpgsqlDbType.Integer).Value = seatNumber;

                    object? seatObj = seatCmd.ExecuteScalar();
                    if (seatObj is null || seatObj is DBNull)
                    {
                        Console.WriteLine($"Место №{seatNumber} не существует в зале '{hallName}' (ID зала: {hallId}).");
                        transaction.Rollback();
                        return;
                    }

                    seatId = Convert.ToInt32(seatObj, CultureInfo.InvariantCulture);
                }

                const string checkTicketSql = @"
                    SELECT COUNT(1)
                    FROM tickets
                    WHERE screening_id = @screening_id
                      AND seat_id = @seat_id
                      AND status = 'Purchased';";

                using (var activeTicketCmd = new NpgsqlCommand(checkTicketSql, connection, transaction))
                {
                    activeTicketCmd.Parameters.Add("@screening_id", NpgsqlDbType.Integer).Value = screeningId;
                    activeTicketCmd.Parameters.Add("@seat_id", NpgsqlDbType.Integer).Value = seatId;

                    long activeCount = Convert.ToInt64(activeTicketCmd.ExecuteScalar(), CultureInfo.InvariantCulture);
                    if (activeCount > 0)
                    {
                        Console.WriteLine($"Место №{seatNumber} на сеанс №{screeningId} уже занято.");
                        transaction.Rollback();
                        return;
                    }
                }

                const string insertTicketSql = @"
                    INSERT INTO tickets (screening_id, hall_id, seat_id, customer_id, status, purchased_at)
                    VALUES (@screening_id, @hall_id, @seat_id, @customer_id, 'Purchased', CURRENT_TIMESTAMP)
                    RETURNING ticket_id;";

                int ticketId;
                using (var insertCmd = new NpgsqlCommand(insertTicketSql, connection, transaction))
                {
                    insertCmd.Parameters.Add("@screening_id", NpgsqlDbType.Integer).Value = screeningId;
                    insertCmd.Parameters.Add("@hall_id", NpgsqlDbType.Integer).Value = hallId;
                    insertCmd.Parameters.Add("@seat_id", NpgsqlDbType.Integer).Value = seatId;
                    insertCmd.Parameters.Add("@customer_id", NpgsqlDbType.Integer).Value = customerId;

                    object? newTicketObj = insertCmd.ExecuteScalar();
                    ticketId = Convert.ToInt32(newTicketObj, CultureInfo.InvariantCulture);
                }

                transaction.Commit();

                Console.WriteLine("=== Подтверждение покупки билета ===");
                Console.WriteLine($"Номер билета : {ticketId}");
                Console.WriteLine($"Фильм        : {filmTitle}");
                Console.WriteLine($"Дата сеанса  : {startsAt:yyyy-MM-dd HH:mm}");
                Console.WriteLine($"Зал и место  : {hallName}, место №{seatNumber}");
                Console.WriteLine($"Цена         : {ticketPrice:F2}");
            }
            catch (PostgresException pgEx) when (pgEx.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                SafeRollback(transaction);
                Console.WriteLine($"Не удалось оформить билет: место №{seatNumber} на выбранном сеансе уже занято другим покупателем.");
            }
            catch (PostgresException pgEx)
            {
                SafeRollback(transaction);
                PrintDatabaseError("Ошибка БД при оформлении покупки билета. Изменения отменены.", pgEx);
            }
            catch (Exception ex)
            {
                SafeRollback(transaction);
                PrintGeneralError("Ошибка при оформлении покупки билета. Изменения отменены.", ex);
            }
        }
        catch (PostgresException pgEx)
        {
            PrintDatabaseError("Ошибка подключения к БД при покупке билета.", pgEx);
        }
        catch (Exception ex)
        {
            PrintGeneralError("Не удалось подключиться к БД для покупки билета.", ex);
        }
    }

    private static void CancelTicket()
    {
        if (!TryReadPositiveInt("Введите номер билета (ticket_id) для отмены: ", out int ticketId))
        {
            return;
        }

        const string updateSql = @"
            UPDATE tickets
            SET status = 'Cancelled'
            WHERE ticket_id = @ticket_id
              AND status = 'Purchased';";

        const string checkStatusSql = @"
            SELECT status
            FROM tickets
            WHERE ticket_id = @ticket_id;";

        try
        {
            using var connection = new NpgsqlConnection(ConnectionString);
            connection.Open();

            using var updateCmd = new NpgsqlCommand(updateSql, connection);
            updateCmd.Parameters.Add("@ticket_id", NpgsqlDbType.Integer).Value = ticketId;

            int affectedRows = updateCmd.ExecuteNonQuery();

            if (affectedRows > 0)
            {
                Console.WriteLine($"Билет №{ticketId} успешно отменён (статус изменён на 'Cancelled'). Место снова свободно.");
                return;
            }

            using var checkCmd = new NpgsqlCommand(checkStatusSql, connection);
            checkCmd.Parameters.Add("@ticket_id", NpgsqlDbType.Integer).Value = ticketId;

            object? existingStatus = checkCmd.ExecuteScalar();
            if (existingStatus is null || existingStatus is DBNull)
            {
                Console.WriteLine($"Билет с номером {ticketId} не найден.");
            }
            else
            {
                Console.WriteLine($"Билет №{ticketId} уже отменён (текущий статус: {existingStatus}).");
            }
        }
        catch (PostgresException pgEx)
        {
            PrintDatabaseError("Ошибка БД при отмене билета.", pgEx);
        }
        catch (Exception ex)
        {
            PrintGeneralError("Не удалось отменить билет.", ex);
        }
    }

    private static void ShowScreeningsReportAndTopFilms()
    {
        const string reportSql = @"
            SELECT
                s.screening_id,
                f.title,
                s.starts_at,
                h.hall_name,
                COUNT(t.ticket_id) AS sold_tickets,
                COALESCE(SUM(CASE WHEN t.ticket_id IS NOT NULL THEN s.ticket_price ELSE 0::numeric END), 0::numeric) AS revenue
            FROM screenings AS s
            JOIN films AS f ON f.film_id = s.film_id
            JOIN halls AS h ON h.hall_id = s.hall_id
            LEFT JOIN tickets AS t
                ON t.screening_id = s.screening_id
               AND t.status = 'Purchased'
            GROUP BY
                s.screening_id,
                f.title,
                s.starts_at,
                h.hall_name
            ORDER BY s.starts_at, s.screening_id;";

        const string topFilmsSql = @"
            SELECT
                f.film_id,
                f.title,
                COUNT(t.ticket_id) AS sold_tickets,
                COALESCE(SUM(CASE WHEN t.ticket_id IS NOT NULL THEN s.ticket_price ELSE 0::numeric END), 0::numeric) AS revenue
            FROM films AS f
            LEFT JOIN screenings AS s ON s.film_id = f.film_id
            LEFT JOIN tickets AS t
                ON t.screening_id = s.screening_id
               AND t.status = 'Purchased'
            GROUP BY f.film_id, f.title
            ORDER BY sold_tickets DESC, revenue DESC, f.title
            LIMIT 3;";

        try
        {
            using var connection = new NpgsqlConnection(ConnectionString);
            connection.Open();

            Console.WriteLine("=== Отчёт по сеансам (только купленные билеты Purchased) ===");
            Console.WriteLine($"{"ID",-4} | {"Фильм",-26} | {"Дата и время",-16} | {"Зал",-7} | {"Продано",-8} | {"Выручка",-10}");
            Console.WriteLine(new string('-', 84));

            decimal totalRevenue = 0m;
            long totalSold = 0;

            using (var reportCmd = new NpgsqlCommand(reportSql, connection))
            using (NpgsqlDataReader reader = reportCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    int screeningId = reader.GetInt32(0);
                    string title = reader.GetString(1);
                    DateTime startsAt = reader.GetDateTime(2);
                    string hallName = reader.GetString(3);
                    long soldTickets = reader.GetInt64(4);
                    decimal revenue = reader.GetDecimal(5);

                    totalSold += soldTickets;
                    totalRevenue += revenue;

                    Console.WriteLine(
                        $"{screeningId,-4} | {title,-26} | {startsAt:yyyy-MM-dd HH:mm,-16} | {hallName,-7} | {soldTickets,8} | {revenue,10:F2}");
                }
            }

            Console.WriteLine(new string('-', 84));
            Console.WriteLine($"ИТОГО продано билетов: {totalSold}, общая выручка: {totalRevenue:F2}");
            Console.WriteLine();

            Console.WriteLine("=== Топ-3 фильма по количеству проданных билетов ===");
            Console.WriteLine($"{"Место",-5} | {"ID",-4} | {"Фильм",-28} | {"Продано билетов",-15} | {"Выручка",-10}");
            Console.WriteLine(new string('-', 72));

            using (var topCmd = new NpgsqlCommand(topFilmsSql, connection))
            using (NpgsqlDataReader topReader = topCmd.ExecuteReader())
            {
                int rank = 1;
                while (topReader.Read())
                {
                    int filmId = topReader.GetInt32(0);
                    string title = topReader.GetString(1);
                    long soldTickets = topReader.GetInt64(2);
                    decimal revenue = topReader.GetDecimal(3);

                    Console.WriteLine($"{rank,-5} | {filmId,-4} | {title,-28} | {soldTickets,15} | {revenue,10:F2}");
                    rank++;
                }
            }
        }
        catch (PostgresException pgEx)
        {
            PrintDatabaseError("Ошибка БД при формировании отчёта.", pgEx);
        }
        catch (Exception ex)
        {
            PrintGeneralError("Не удалось сформировать отчёт.", ex);
        }
    }

    private static bool TryReadPositiveInt(string prompt, out int value)
    {
        Console.Write(prompt);
        string? input = Console.ReadLine()?.Trim();
        if (!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value <= 0)
        {
            Console.WriteLine("Ошибка ввода: необходимо ввести положительное целое число.");
            value = 0;
            return false;
        }

        return true;
    }

    private static bool TryReadDate(string prompt, out DateTime date)
    {
        Console.Write(prompt);
        string? input = Console.ReadLine()?.Trim();
        if (!DateTime.TryParseExact(
                input,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date))
        {
            Console.WriteLine("Ошибка ввода: дата должна быть в формате ГГГГ-ММ-ДД (например, 2026-10-27).");
            return false;
        }

        return true;
    }

    private static void SafeRollback(NpgsqlTransaction transaction)
    {
        try
        {
            transaction.Rollback();
        }
        catch
        {
        }
    }

    private static void PrintDatabaseError(string userMessage, PostgresException pgEx)
    {
        Console.WriteLine($"[Ошибка] {userMessage}");
        Console.WriteLine($"[Детали для разработчика] SQLState={pgEx.SqlState}, Message={pgEx.MessageText}");
    }

    private static void PrintGeneralError(string userMessage, Exception ex)
    {
        Console.WriteLine($"[Ошибка] {userMessage}");
        Console.WriteLine($"[Детали для разработчика] {ex.GetType().Name}: {ex.Message}");
    }
}

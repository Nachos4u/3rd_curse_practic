using System.Data;
using Dapper;

namespace Restaurant.Data;

/// <summary>
/// Обработчики типов Dapper для <see cref="TimeOnly"/> и <see cref="DateOnly"/>.
/// Текущая версия Dapper не умеет передавать эти типы как параметры команды,
/// а Npgsql возвращает их при чтении столбцов <c>time</c>/<c>date</c>.
/// Обработчики обеспечивают корректную передачу и чтение в обе стороны.
/// </summary>
internal sealed class TimeOnlyHandler : SqlMapper.TypeHandler<TimeOnly>
{
    public override TimeOnly Parse(object value) => value switch
    {
        TimeOnly t => t,
        TimeSpan ts => TimeOnly.FromTimeSpan(ts),
        DateTime dt => TimeOnly.FromDateTime(dt),
        _ => TimeOnly.Parse(value.ToString()!)
    };

    // Npgsql самостоятельно сопоставляет TimeOnly с типом PostgreSQL time.
    public override void SetValue(IDbDataParameter parameter, TimeOnly value)
        => parameter.Value = value;
}

internal sealed class DateOnlyHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override DateOnly Parse(object value) => value switch
    {
        DateOnly d => d,
        DateTime dt => DateOnly.FromDateTime(dt),
        _ => DateOnly.Parse(value.ToString()!)
    };

    public override void SetValue(IDbDataParameter parameter, DateOnly value)
        => parameter.Value = value;
}

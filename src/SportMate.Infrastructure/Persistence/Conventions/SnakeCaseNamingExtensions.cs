using System.Text;
using Microsoft.EntityFrameworkCore;

namespace SportMate.Infrastructure.Persistence.Conventions;

internal static class SnakeCaseNamingExtensions
{
    /// <summary>
    /// Maps property names to snake_case columns and lower-cases key, foreign key and index names.
    /// Table names are set explicitly in each entity configuration.
    /// </summary>
    public static void ApplySnakeCaseNames(this ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }

            foreach (var key in entity.GetKeys())
            {
                key.SetName(key.GetDefaultName()?.ToLowerInvariant());
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                foreignKey.SetConstraintName(foreignKey.GetDefaultName()?.ToLowerInvariant());
            }

            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(index.GetDefaultDatabaseName()?.ToLowerInvariant());
            }
        }
    }

    internal static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];
            if (char.IsUpper(current) && i > 0)
            {
                var previous = name[i - 1];
                var nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);
                if (char.IsLower(previous) || char.IsDigit(previous) || (char.IsUpper(previous) && nextIsLower))
                {
                    builder.Append('_');
                }
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }
}

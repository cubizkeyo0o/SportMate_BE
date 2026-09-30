# Local MySQL

Install and run MySQL 8 locally. Create a development database and a least-privilege account (replace the placeholders with locally chosen credentials):

```sql
CREATE DATABASE sportmate CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER 'sportmate_app'@'localhost' IDENTIFIED BY '<your-local-password>';
GRANT ALL PRIVILEGES ON sportmate.* TO 'sportmate_app'@'localhost';
```

Set `ConnectionStrings__DefaultConnection` in the environment to a MySQL connection string. Do not commit passwords. Then apply the EF Core migrations as described in the backend README.

## Development seed data

For sample courts to try court discovery locally, see [seed/README.md](seed/README.md).

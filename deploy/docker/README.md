# Local Docker Environment

Milestone 0 uses the root `docker-compose.yml` file for local PostgreSQL.

```bash
docker compose up -d postgres
```

The development database listens on `localhost:54329` with:

- Database: `kalm`
- User: `kalm`
- Password: `kalm_dev_password`

These credentials are development-only and must not be reused for staging or production.

import "dotenv/config";
import { defineConfig } from "drizzle-kit";

export default defineConfig({
    out: "./drizzle",
    schema: "./src/db/schema.ts",
    dialect: "postgresql",
    dbCredentials: {
        database: process.env.DB_NAME ?? "cars",
        host: process.env.DB_HOST ?? "localhost",
        password: process.env.DB_PASSWORD ?? "password",
        port: Number(process.env.DB_PORT) ?? 5432,
        user: process.env.DB_USER ?? "user",
        ssl: false,
    },
});

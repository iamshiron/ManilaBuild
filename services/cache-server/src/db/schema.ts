import { integer, pgTable, varchar } from "drizzle-orm/pg-core";

export const Users = pgTable("users", {
    id: integer("id").primaryKey().generatedByDefaultAsIdentity(),
    name: varchar("name", { length: 256 }),
    key: varchar("token", { length: 256 }).unique(),
});

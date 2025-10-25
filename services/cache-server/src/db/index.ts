import { drizzle } from "drizzle-orm/node-postgres";
import * as Schema from "./schema";

const DATABASE_URL = `postgresql://${process.env.DB_USER}:${process.env.DB_PASSWORD}@${process.env.DB_HOST}:${process.env.DB_PORT}/${process.env.DB_NAME}`;
const db = drizzle(DATABASE_URL, { schema: Schema });
export default db;

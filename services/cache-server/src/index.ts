import { Elysia } from "elysia";

const app = new Elysia().get("/", () => "Hello Elysia").listen(process.env.PORT ?? 3000);

console.log(
    `🌆 Manila Cache Server is running at: ${app.server?.hostname}:${app.server?.port}`,
);

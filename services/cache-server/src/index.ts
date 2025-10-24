import { Elysia } from "elysia";
import { openapi } from "@elysiajs/openapi";
import { authMiddleware } from "./auth";

const dev = process.env.NODE_ENV !== "production";

const app = new Elysia();
const doc = new Elysia();

if (dev) doc.use(openapi());

app.use(doc)
    .onRequest(authMiddleware)
    .get("/artifacts", () => {
        return [];
    })
    .put("/artifacts/:key", () => {})
    .post("/artifacts/:key", () => {})
    .get("/artifacts/:key", () => {})
    .listen(process.env.PORT ?? 3000);

console.log(
    `🌆 Manila Cache Server is running at: ${app.server?.hostname}:${app.server?.port}`,
);

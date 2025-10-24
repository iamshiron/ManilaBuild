import { status, type Context } from "elysia";

const AUTH_TOKEN = process.env.AUTH_TOKEN;

export async function authMiddleware({ request }: { request: Request }) {
    if (
        request.url.endsWith("/health") ||
        request.url.endsWith("/openapi/json")
    ) {
        return;
    }

    if (!AUTH_TOKEN)
        throw new Error("AUTH_TOKEN is not set in environment variables");

    const authHeader = request.headers.get("Authorization");
    if (!authHeader || authHeader !== `Bearer ${AUTH_TOKEN}`) {
        return status(401);
    }
}

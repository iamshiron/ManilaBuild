import { Elysia, t } from "elysia";
import { openapi } from "@elysiajs/openapi";
import { authMiddleware } from "./auth";
import db from "./db";
import { ArtifactOutput, Artifacts } from "./db/schema";
import { eq } from "drizzle-orm";
import FS from "node:fs";

const dev = process.env.NODE_ENV !== "production";

const app = new Elysia();

if (dev)
    app.use(
        openapi({
            provider: "scalar",
        }),
    );

if (process.env.NO_AUTH === undefined || process.env.NO_AUTH === "false")
    app.onRequest(authMiddleware);
else console.warn("⚠️  Running cache server without authentication!");

app.get(
    "/artifacts",
    async (c) => {
        const artifacts = await db
            .select()
            .from(Artifacts)
            .leftJoin(
                ArtifactOutput,
                eq(Artifacts.id, ArtifactOutput.artifactID),
            );

        FS.writeFileSync(
            "artifacts_debug.json",
            JSON.stringify(artifacts, null, 4),
        );

        return c.status(200, []);
    },
    {
        response: {
            200: t.Array(
                t.Object({
                    name: t.String(),
                    project: t.String(),
                    hash: t.String(),
                    outputs: t.Array(
                        t.Object({
                            id: t.Number(),
                            sizeB: t.Number(),
                        }),
                    ),
                }),
            ),
        },
    },
)
    .put(
        "/artifacts/:key",
        async (c) => {
            await db.insert(Artifacts).values({
                artifact: c.body.name,
                project: c.body.project,
                type: c.body.type,
                hash: c.params.key,
            });
        },
        {
            body: t.Object({
                name: t.String(),
                project: t.String(),
                type: t.String(),
            }),
            response: {
                201: t.Object({
                    id: t.Number(),
                    message: t.String(),
                }),
                401: t.Object({
                    error: t.String(),
                }),
            },
        },
    )
    .get(
        "/artifacts/:key",
        async () => {
            return [];
        },
        {
            response: {
                200: t.Array(
                    t.Object({
                        id: t.Number(),
                        sizeB: t.Number(),
                    }),
                ),
                401: t.Object({
                    error: t.String(),
                }),
            },
        },
    )
    .post(
        "/artifacts/:key/output",
        async (c) => {
            const [artifact] = await db
                .select({ id: Artifacts.id })
                .from(Artifacts)
                .where(eq(Artifacts.hash, c.params.key));

            if (!artifact) {
                return c.status(404, { error: "Artifact not found" });
            }

            const buffer = Buffer.from(await c.body.arrayBuffer());
            await db.insert(ArtifactOutput).values({
                artifactID: artifact.id,
                data: buffer,
                sizeB: buffer.length,
            });
        },
        {
            body: t.File({ type: "application/zip" }),
            response: {
                201: t.Object({
                    id: t.Number(),
                    message: t.String(),
                }),
                401: t.Object({
                    error: t.String(),
                }),
                404: t.Object({
                    error: t.String(),
                }),
            },
        },
    )
    .listen(process.env.PORT ?? 3000);

console.log(
    `🌆 Manila Cache Server is running at: ${app.server?.hostname}:${app.server?.port}`,
);

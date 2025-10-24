import { relations } from "drizzle-orm";
import {
    integer,
    jsonb,
    pgEnum,
    pgTable,
    timestamp,
    varchar,
} from "drizzle-orm/pg-core";

export const LogLevel = pgEnum("log_level", [
    "System",
    "Debug",
    "Info",
    "Warning",
    "Error",
    "Critical",
]);

export const Artifacts = pgTable("artifacts", {
    id: integer("id").primaryKey().generatedByDefaultAsIdentity(),
    project: varchar("project", { length: 255 }),
    artifact: varchar("artifact", { length: 255 }),
    hash: varchar("hash", { length: 255 }),
    createdAt: timestamp("created_at").notNull().defaultNow(),
    lastAccessedAt: timestamp("last_accessed_at").notNull().defaultNow(),
    type: varchar("type", { length: 256 }).notNull(),
});
export const ArtifactsRelations = relations(Artifacts, ({ many }) => ({
    outputs: many(ArtifactOutput),
    logEntries: many(LogEntry),
}));

export const ArtifactOutput = pgTable("artifact_outputs", {
    id: integer("id").primaryKey().generatedByDefaultAsIdentity(),
    artifactID: integer("artifact_id").references(() => Artifacts.id),
});
export const ArtifactOutputRelations = relations(ArtifactOutput, ({ one }) => ({
    artifact: one(Artifacts, {
        fields: [ArtifactOutput.artifactID],
        references: [Artifacts.id],
    }),
}));

export const LogEntry = pgTable("log_entries", {
    id: integer("id").primaryKey().generatedByDefaultAsIdentity(),
    artifactID: integer("artifact_id")
        .references(() => Artifacts.id)
        .notNull(),
    level: LogLevel("level").notNull(),
    timestamp: timestamp("timestamp").notNull(),
    data: jsonb("data").notNull(),
});
export const LogEntryRelations = relations(LogEntry, ({ one }) => ({
    artifact: one(Artifacts, {
        fields: [LogEntry.artifactID],
        references: [Artifacts.id],
    }),
}));

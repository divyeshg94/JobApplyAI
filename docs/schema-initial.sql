IF OBJECT_ID(N'[jobapply].[__EFMigrationsHistory_JobApply]') IS NULL
BEGIN
    IF SCHEMA_ID(N'jobapply') IS NULL EXEC(N'CREATE SCHEMA [jobapply];');
    CREATE TABLE [jobapply].[__EFMigrationsHistory_JobApply] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory_JobApply] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    IF SCHEMA_ID(N'jobapply') IS NULL EXEC(N'CREATE SCHEMA [jobapply];');
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    CREATE TABLE [jobapply].[JobPostings] (
        [Id] uniqueidentifier NOT NULL,
        [Source] int NOT NULL,
        [ExternalJobId] nvarchar(200) NOT NULL,
        [Title] nvarchar(300) NOT NULL,
        [CompanyName] nvarchar(200) NOT NULL,
        [LocationText] nvarchar(300) NULL,
        [DescriptionText] nvarchar(max) NULL,
        [ApplyUrl] nvarchar(1000) NOT NULL,
        [PostedAtUtc] datetimeoffset NULL,
        [FetchedAtUtc] datetimeoffset NOT NULL,
        [RawJsonPayload] json NOT NULL,
        [IsActive] bit NOT NULL,
        [JobEmbedding] vector(1536) NULL,
        CONSTRAINT [PK_JobPostings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    CREATE TABLE [jobapply].[Users] (
        [Id] uniqueidentifier NOT NULL,
        [Email] nvarchar(320) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    CREATE TABLE [jobapply].[CandidateProfiles] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL,
        [RawResumeBlobUrl] nvarchar(1000) NULL,
        [RawResumeFileName] nvarchar(300) NULL,
        [RawResumeContentType] nvarchar(100) NULL,
        [FullName] nvarchar(200) NULL,
        [Email] nvarchar(320) NULL,
        [Phone] nvarchar(50) NULL,
        [LocationText] nvarchar(300) NULL,
        [LinkedInUrl] nvarchar(500) NULL,
        [PortfolioUrl] nvarchar(500) NULL,
        [SummaryText] nvarchar(max) NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [ParsedAtUtc] datetimeoffset NULL,
        [ReviewedAtUtc] datetimeoffset NULL,
        [ProfileEmbedding] vector(1536) NULL,
        CONSTRAINT [PK_CandidateProfiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CandidateProfiles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [jobapply].[Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    CREATE TABLE [jobapply].[JobSourceSubscriptions] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Source] int NOT NULL,
        [ConfigJson] json NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [IsEnabled] bit NOT NULL,
        [LastPolledAtUtc] datetimeoffset NULL,
        [LastPollStatus] int NULL,
        [LastPollError] nvarchar(2000) NULL,
        CONSTRAINT [PK_JobSourceSubscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_JobSourceSubscriptions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [jobapply].[Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    CREATE TABLE [jobapply].[MatchResults] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [JobPostingId] uniqueidentifier NOT NULL,
        [CandidateProfileId] uniqueidentifier NOT NULL,
        [VectorScore] float NOT NULL,
        [LlmScore] float NOT NULL,
        [LlmReasoning] nvarchar(max) NULL,
        [Status] int NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [NotifiedAtUtc] datetimeoffset NULL,
        CONSTRAINT [PK_MatchResults] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MatchResults_CandidateProfiles_CandidateProfileId] FOREIGN KEY ([CandidateProfileId]) REFERENCES [jobapply].[CandidateProfiles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MatchResults_JobPostings_JobPostingId] FOREIGN KEY ([JobPostingId]) REFERENCES [jobapply].[JobPostings] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_MatchResults_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [jobapply].[Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    CREATE TABLE [jobapply].[ProfileEducations] (
        [Id] uniqueidentifier NOT NULL,
        [CandidateProfileId] uniqueidentifier NOT NULL,
        [Institution] nvarchar(300) NOT NULL,
        [Degree] nvarchar(200) NULL,
        [FieldOfStudy] nvarchar(200) NULL,
        [StartDate] date NULL,
        [EndDate] date NULL,
        CONSTRAINT [PK_ProfileEducations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProfileEducations_CandidateProfiles_CandidateProfileId] FOREIGN KEY ([CandidateProfileId]) REFERENCES [jobapply].[CandidateProfiles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    CREATE TABLE [jobapply].[ProfileSkills] (
        [Id] uniqueidentifier NOT NULL,
        [CandidateProfileId] uniqueidentifier NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Category] nvarchar(100) NULL,
        CONSTRAINT [PK_ProfileSkills] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProfileSkills_CandidateProfiles_CandidateProfileId] FOREIGN KEY ([CandidateProfileId]) REFERENCES [jobapply].[CandidateProfiles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    CREATE TABLE [jobapply].[ProfileWorkExperiences] (
        [Id] uniqueidentifier NOT NULL,
        [CandidateProfileId] uniqueidentifier NOT NULL,
        [Company] nvarchar(200) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [LocationText] nvarchar(300) NULL,
        [StartDate] date NULL,
        [EndDate] date NULL,
        [IsCurrent] bit NOT NULL,
        [DescriptionText] nvarchar(max) NULL,
        CONSTRAINT [PK_ProfileWorkExperiences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProfileWorkExperiences_CandidateProfiles_CandidateProfileId] FOREIGN KEY ([CandidateProfileId]) REFERENCES [jobapply].[CandidateProfiles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    CREATE TABLE [jobapply].[PollRunLogs] (
        [Id] uniqueidentifier NOT NULL,
        [JobSourceSubscriptionId] uniqueidentifier NOT NULL,
        [StartedAtUtc] datetimeoffset NOT NULL,
        [CompletedAtUtc] datetimeoffset NULL,
        [JobsFetched] int NOT NULL,
        [JobsNew] int NOT NULL,
        [JobsFailed] int NOT NULL,
        [ErrorMessage] nvarchar(4000) NULL,
        CONSTRAINT [PK_PollRunLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PollRunLogs_JobSourceSubscriptions_JobSourceSubscriptionId] FOREIGN KEY ([JobSourceSubscriptionId]) REFERENCES [jobapply].[JobSourceSubscriptions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    CREATE TABLE [jobapply].[Applications] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [MatchResultId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL,
        [TailoredResumeBlobUrl] nvarchar(1000) NULL,
        [TailoredCoverLetterBlobUrl] nvarchar(1000) NULL,
        [GeneratedAtUtc] datetimeoffset NULL,
        [AppliedAtUtc] datetimeoffset NULL,
        [Notes] nvarchar(4000) NULL,
        CONSTRAINT [PK_Applications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Applications_MatchResults_MatchResultId] FOREIGN KEY ([MatchResultId]) REFERENCES [jobapply].[MatchResults] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Applications_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [jobapply].[Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'DisplayName', N'Email') AND [object_id] = OBJECT_ID(N'[jobapply].[Users]'))
        SET IDENTITY_INSERT [jobapply].[Users] ON;
    EXEC(N'INSERT INTO [jobapply].[Users] ([Id], [CreatedAtUtc], [DisplayName], [Email])
    VALUES (''a1e0c8f0-0000-4000-8000-000000000001'', ''2026-01-01T00:00:00.0000000+00:00'', N''Default User'', N''owner@localhost'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'DisplayName', N'Email') AND [object_id] = OBJECT_ID(N'[jobapply].[Users]'))
        SET IDENTITY_INSERT [jobapply].[Users] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Applications_MatchResultId] ON [jobapply].[Applications] ([MatchResultId]);
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Applications_UserId_Status] ON [jobapply].[Applications] ([UserId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CandidateProfiles_UserId_Status] ON [jobapply].[CandidateProfiles] ([UserId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_JobPostings_Source_ExternalJobId] ON [jobapply].[JobPostings] ([Source], [ExternalJobId]);
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_JobSourceSubscriptions_UserId_IsEnabled] ON [jobapply].[JobSourceSubscriptions] ([UserId], [IsEnabled]);
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MatchResults_CandidateProfileId] ON [jobapply].[MatchResults] ([CandidateProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MatchResults_JobPostingId] ON [jobapply].[MatchResults] ([JobPostingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MatchResults_UserId_JobPostingId] ON [jobapply].[MatchResults] ([UserId], [JobPostingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MatchResults_UserId_Status] ON [jobapply].[MatchResults] ([UserId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PollRunLogs_JobSourceSubscriptionId] ON [jobapply].[PollRunLogs] ([JobSourceSubscriptionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PollRunLogs_StartedAtUtc] ON [jobapply].[PollRunLogs] ([StartedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProfileEducations_CandidateProfileId] ON [jobapply].[ProfileEducations] ([CandidateProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProfileSkills_CandidateProfileId] ON [jobapply].[ProfileSkills] ([CandidateProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProfileWorkExperiences_CandidateProfileId] ON [jobapply].[ProfileWorkExperiences] ([CandidateProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260717012910_InitialCreate'
)
BEGIN
    INSERT INTO [jobapply].[__EFMigrationsHistory_JobApply] ([MigrationId], [ProductVersion])
    VALUES (N'20260717012910_InitialCreate', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260719161800_AddMatchingPreferences'
)
BEGIN
    ALTER TABLE [jobapply].[JobPostings] ADD [SalaryMaxAnnualUsd] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260719161800_AddMatchingPreferences'
)
BEGIN
    ALTER TABLE [jobapply].[JobPostings] ADD [SalaryMinAnnualUsd] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260719161800_AddMatchingPreferences'
)
BEGIN
    ALTER TABLE [jobapply].[JobPostings] ADD [VisaSponsorship] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260719161800_AddMatchingPreferences'
)
BEGIN
    ALTER TABLE [jobapply].[CandidateProfiles] ADD [MinimumSalaryUsd] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260719161800_AddMatchingPreferences'
)
BEGIN
    ALTER TABLE [jobapply].[CandidateProfiles] ADD [RequiresVisaSponsorship] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260719161800_AddMatchingPreferences'
)
BEGIN
    CREATE TABLE [jobapply].[ProfileExcludedCompanies] (
        [Id] uniqueidentifier NOT NULL,
        [CandidateProfileId] uniqueidentifier NOT NULL,
        [CompanyName] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_ProfileExcludedCompanies] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProfileExcludedCompanies_CandidateProfiles_CandidateProfileId] FOREIGN KEY ([CandidateProfileId]) REFERENCES [jobapply].[CandidateProfiles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260719161800_AddMatchingPreferences'
)
BEGIN
    CREATE INDEX [IX_ProfileExcludedCompanies_CandidateProfileId] ON [jobapply].[ProfileExcludedCompanies] ([CandidateProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260719161800_AddMatchingPreferences'
)
BEGIN
    INSERT INTO [jobapply].[__EFMigrationsHistory_JobApply] ([MigrationId], [ProductVersion])
    VALUES (N'20260719161800_AddMatchingPreferences', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260816164122_AddApplicationDeadlineAndClassifiedAt'
)
BEGIN
    ALTER TABLE [jobapply].[JobPostings] ADD [ApplicationDeadline] date NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260816164122_AddApplicationDeadlineAndClassifiedAt'
)
BEGIN
    ALTER TABLE [jobapply].[JobPostings] ADD [ClassifiedAtUtc] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260816164122_AddApplicationDeadlineAndClassifiedAt'
)
BEGIN
    INSERT INTO [jobapply].[__EFMigrationsHistory_JobApply] ([MigrationId], [ProductVersion])
    VALUES (N'20260816164122_AddApplicationDeadlineAndClassifiedAt', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260816165827_AddWorkLocationCountry'
)
BEGIN
    ALTER TABLE [jobapply].[JobPostings] ADD [WorkLocationCountry] nvarchar(10) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260816165827_AddWorkLocationCountry'
)
BEGIN
    ALTER TABLE [jobapply].[CandidateProfiles] ADD [RequiredCountry] nvarchar(10) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [jobapply].[__EFMigrationsHistory_JobApply]
    WHERE [MigrationId] = N'20260816165827_AddWorkLocationCountry'
)
BEGIN
    INSERT INTO [jobapply].[__EFMigrationsHistory_JobApply] ([MigrationId], [ProductVersion])
    VALUES (N'20260816165827_AddWorkLocationCountry', N'10.0.10');
END;

COMMIT;
GO


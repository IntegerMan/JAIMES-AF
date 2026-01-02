namespace MattEland.Jaimes.Repositories;

public class JaimesDbContext(DbContextOptions<JaimesDbContext> options) : DbContext(options)
{
    public DbSet<Game> Games { get; set; } = null!;
    public DbSet<Message> Messages { get; set; } = null!;
    public DbSet<MessageEmbedding> MessageEmbeddings { get; set; } = null!;
    public DbSet<MessageEvaluationMetric> MessageEvaluationMetrics { get; set; } = null!;
    public DbSet<MessageSentiment> MessageSentiments { get; set; } = null!;
    public DbSet<Model> Models { get; set; } = null!;
    public DbSet<Player> Players { get; set; } = null!;
    public DbSet<Scenario> Scenarios { get; set; } = null!;
    public DbSet<Ruleset> Rulesets { get; set; } = null!;
    public DbSet<ChatHistory> ChatHistories { get; set; } = null!;
    public DbSet<EvaluationExecution> EvaluationExecutions { get; set; } = null!;
    public DbSet<EvaluationScenarioIteration> EvaluationScenarioIterations { get; set; } = null!;

    // Agent instruction system entities
    public DbSet<Agent> Agents { get; set; } = null!;
    public DbSet<AgentInstructionVersion> AgentInstructionVersions { get; set; } = null!;
    public DbSet<ScenarioAgent> ScenarioAgents { get; set; } = null!;

    // Document storage entities (replacing MongoDB)
    public DbSet<DocumentMetadata> DocumentMetadata { get; set; } = null!;
    public DbSet<CrackedDocument> CrackedDocuments { get; set; } = null!;
    public DbSet<DocumentChunk> DocumentChunks { get; set; } = null!;

    // RAG search diagnostic entities
    public DbSet<RagSearchQuery> RagSearchQueries { get; set; } = null!;
    public DbSet<RagSearchResultChunk> RagSearchResultChunks { get; set; } = null!;

    // Message feedback entity
    public DbSet<MessageFeedback> MessageFeedbacks { get; set; } = null!;

    // Message tool call entity
    public DbSet<MessageToolCall> MessageToolCalls { get; set; } = null!;

    public DbSet<Tool> Tools { get; set; } = null!;
    public DbSet<Evaluator> Evaluators { get; set; } = null!;

    // Location tracking entities for AI storyteller
    public DbSet<Location> Locations { get; set; } = null!;
    public DbSet<LocationEvent> LocationEvents { get; set; } = null!;
    public DbSet<NearbyLocation> NearbyLocations { get; set; } = null!;

    // Test case entities for agent evaluation
    public DbSet<TestCase> TestCases { get; set; } = null!;
    public DbSet<TestCaseRun> TestCaseRuns { get; set; } = null!;
    public DbSet<TestCaseRunMetric> TestCaseRunMetrics { get; set; } = null!;
    // File storage for reports and other binary content
    public DbSet<StoredFile> StoredFiles { get; set; } = null!;
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Enable pgvector extension for PostgreSQL (skip for in-memory database used in tests)
        // Only call HasPostgresExtension if we're using the Npgsql provider
        if (string.Equals(Database.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
            modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<Ruleset>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id).IsRequired();
            entity.Property(r => r.Name).IsRequired();
        });

        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Id).IsRequired();
            entity.Property(p => p.RulesetId).IsRequired();
            entity.Property(p => p.Name).IsRequired();

            entity.HasOne(p => p.Ruleset)
                .WithMany(r => r.Players)
                .HasForeignKey(p => p.RulesetId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Scenario>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Id).IsRequired();
            entity.Property(s => s.RulesetId).IsRequired();
            entity.Property(s => s.Name).IsRequired();

            entity.HasOne(s => s.Ruleset)
                .WithMany(r => r.Scenarios)
                .HasForeignKey(s => s.RulesetId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Agent>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Id).IsRequired();
            entity.Property(a => a.Name).IsRequired();
            entity.Property(a => a.Role).IsRequired();
        });

        modelBuilder.Entity<AgentInstructionVersion>(entity =>
        {
            entity.HasKey(iv => iv.Id);
            entity.Property(iv => iv.AgentId).IsRequired();
            entity.Property(iv => iv.VersionNumber).IsRequired();
            entity.Property(iv => iv.Instructions).IsRequired();
            entity.Property(iv => iv.CreatedAt).IsRequired();

            entity.HasOne(iv => iv.Agent)
                .WithMany(a => a.InstructionVersions)
                .HasForeignKey(iv => iv.AgentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(iv => iv.Model)
                .WithMany(m => m.AgentInstructionVersions)
                .HasForeignKey(iv => iv.ModelId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(iv => new {iv.AgentId, iv.VersionNumber}).IsUnique();
        });

        modelBuilder.Entity<ScenarioAgent>(entity =>
        {
            entity.HasKey(sa => new {sa.ScenarioId, sa.AgentId});
            entity.Property(sa => sa.ScenarioId).IsRequired();
            entity.Property(sa => sa.AgentId).IsRequired();
            entity.Property(sa => sa.InstructionVersionId);

            entity.HasOne(sa => sa.Scenario)
                .WithMany(s => s.ScenarioAgents)
                .HasForeignKey(sa => sa.ScenarioId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(sa => sa.Agent)
                .WithMany(a => a.ScenarioAgents)
                .HasForeignKey(sa => sa.AgentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(sa => sa.InstructionVersion)
                .WithMany(iv => iv.ScenarioAgents)
                .HasForeignKey(sa => sa.InstructionVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasKey(g => g.Id);
            entity.Property(g => g.RulesetId).IsRequired();
            entity.Property(g => g.ScenarioId).IsRequired();
            entity.Property(g => g.PlayerId).IsRequired();
            entity.Property(g => g.CreatedAt).IsRequired();

            entity.HasOne(g => g.Ruleset)
                .WithMany(r => r.Games)
                .HasForeignKey(g => g.RulesetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(g => g.Scenario)
                .WithMany(s => s.Games)
                .HasForeignKey(g => g.ScenarioId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(g => g.Player)
                .WithMany(p => p.Games)
                .HasForeignKey(g => g.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(g => g.MostRecentHistory)
                .WithMany()
                .HasForeignKey(g => g.MostRecentHistoryId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.GameId).IsRequired();
            entity.Property(m => m.Text).IsRequired();
            entity.Property(m => m.CreatedAt).IsRequired();

            entity.HasOne(m => m.Game)
                .WithMany(g => g.Messages)
                .HasForeignKey(m => m.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.ChatHistory)
                .WithMany()
                .HasForeignKey(m => m.ChatHistoryId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(m => m.Agent)
                .WithMany(a => a.Messages)
                .HasForeignKey(m => m.AgentId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(m => m.InstructionVersion)
                .WithMany(iv => iv.Messages)
                .HasForeignKey(m => m.InstructionVersionId)
                .OnDelete(DeleteBehavior.NoAction);

            // Self-referencing foreign keys for message ordering (linked list structure)
            entity.HasOne(m => m.PreviousMessage)
                .WithMany()
                .HasForeignKey(m => m.PreviousMessageId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(m => m.NextMessage)
                .WithMany()
                .HasForeignKey(m => m.NextMessageId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(m => m.Model)
                .WithMany(mo => mo.Messages)
                .HasForeignKey(m => m.ModelId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ChatHistory>(entity =>
        {
            entity.HasKey(ch => ch.Id);
            entity.Property(ch => ch.GameId).IsRequired();
            entity.Property(ch => ch.ThreadJson).IsRequired();
            entity.Property(ch => ch.CreatedAt).IsRequired();

            entity.HasOne(ch => ch.Game)
                .WithMany()
                .HasForeignKey(ch => ch.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ch => ch.PreviousHistory)
                .WithMany()
                .HasForeignKey(ch => ch.PreviousHistoryId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(ch => ch.Message)
                .WithMany()
                .HasForeignKey(ch => ch.MessageId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Document storage entity configurations
        modelBuilder.Entity<DocumentMetadata>(entity =>
        {
            entity.HasKey(dm => dm.Id);
            entity.Property(dm => dm.FilePath).IsRequired();
            entity.Property(dm => dm.Hash).IsRequired();
            entity.Property(dm => dm.LastScanned).IsRequired();
            entity.Property(dm => dm.DocumentKind).IsRequired();
            entity.Property(dm => dm.RulesetId).IsRequired();

            // Create unique index on FilePath for fast lookups
            entity.HasIndex(dm => dm.FilePath).IsUnique();
        });

        modelBuilder.Entity<CrackedDocument>(entity =>
        {
            entity.HasKey(cd => cd.Id);
            entity.Property(cd => cd.FilePath).IsRequired();
            entity.Property(cd => cd.FileName).IsRequired();
            entity.Property(cd => cd.Content).IsRequired();
            entity.Property(cd => cd.CrackedAt).IsRequired();
            entity.Property(cd => cd.DocumentKind).IsRequired();
            entity.Property(cd => cd.RulesetId).IsRequired();

            // Create unique index on FilePath for fast lookups
            entity.HasIndex(cd => cd.FilePath).IsUnique();
        });

        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.HasKey(dc => dc.Id);
            entity.Property(dc => dc.ChunkId).IsRequired();
            entity.Property(dc => dc.DocumentId).IsRequired();
            entity.Property(dc => dc.ChunkText).IsRequired();
            entity.Property(dc => dc.CreatedAt).IsRequired();

            // Configure Embedding property for different database providers
            // For PostgreSQL: pgvector extension handles Vector natively via the [Column(TypeName = "vector")] attribute
            // For other providers (like in-memory): use a value converter to store as byte array
            // Check if database is available (may not be at design-time)
            string providerName;
            try
            {
                providerName = Database.ProviderName ?? "Npgsql.EntityFrameworkCore.PostgreSQL";
            }
            catch
            {
                // At design-time or when database is not initialized, assume PostgreSQL
                providerName = "Npgsql.EntityFrameworkCore.PostgreSQL";
            }

            if (!string.Equals(providerName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
            {
                // For non-PostgreSQL providers (like in-memory), convert Vector to/from byte array
                // Override the column type from the attribute to allow in-memory database to work
                entity.Property(dc => dc.Embedding)
                    .HasConversion(
                        v => v == null ? null : ConvertVectorToBytes(v),
                        b => b == null ? null : ConvertBytesToVector(b))
                    .HasColumnType("varbinary(max)"); // Use varbinary for in-memory database compatibility
            }
            else
            {
                // For PostgreSQL, pgvector.EntityFrameworkCore provides the value converter automatically
                // when UseVector() is called in the Npgsql configuration
                // The [Column(TypeName = "vector")] attribute ensures the correct database type
                // No explicit configuration needed - pgvector handles it
            }

            // Create unique index on ChunkId for fast lookups
            entity.HasIndex(dc => dc.ChunkId).IsUnique();

            // Create index on DocumentId for efficient queries
            entity.HasIndex(dc => dc.DocumentId);

            entity.HasOne(dc => dc.CrackedDocument)
                .WithMany()
                .HasForeignKey(dc => dc.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MessageEmbedding>(entity =>
        {
            entity.HasKey(me => me.Id);
            entity.Property(me => me.MessageId).IsRequired();
            entity.Property(me => me.EmbeddedAt).IsRequired();

            // Configure Embedding property for different database providers
            // For PostgreSQL: pgvector extension handles Vector natively via the [Column(TypeName = "vector")] attribute
            // For other providers (like in-memory): use a value converter to store as byte array
            // Check if database is available (may not be at design-time)
            string providerName;
            try
            {
                providerName = Database.ProviderName ?? "Npgsql.EntityFrameworkCore.PostgreSQL";
            }
            catch
            {
                // At design-time or when database is not initialized, assume PostgreSQL
                providerName = "Npgsql.EntityFrameworkCore.PostgreSQL";
            }

            if (!string.Equals(providerName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
            {
                // For non-PostgreSQL providers (like in-memory), convert Vector to/from byte array
                // Override the column type from the attribute to allow in-memory database to work
                entity.Property(me => me.Embedding)
                    .HasConversion(
                        v => v == null ? null : ConvertVectorToBytes(v),
                        b => b == null ? null : ConvertBytesToVector(b))
                    .HasColumnType("varbinary(max)"); // Use varbinary for in-memory database compatibility
            }
            else
            {
                // For PostgreSQL, pgvector.EntityFrameworkCore provides the value converter automatically
                // when UseVector() is called in the Npgsql configuration
                // The [Column(TypeName = "vector")] attribute ensures the correct database type
                // No explicit configuration needed - pgvector handles it
            }

            // Create unique index on MessageId to ensure one embedding per message
            entity.HasIndex(me => me.MessageId).IsUnique();

            entity.HasOne(me => me.Message)
                .WithOne(m => m.MessageEmbedding)
                .HasForeignKey<MessageEmbedding>(me => me.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MessageSentiment>(entity =>
        {
            entity.HasKey(ms => ms.Id);
            entity.Property(ms => ms.MessageId).IsRequired();
            entity.Property(ms => ms.Sentiment).IsRequired();
            entity.Property(ms => ms.CreatedAt).IsRequired();
            entity.Property(ms => ms.UpdatedAt).IsRequired();

            // Create unique index on MessageId to ensure one sentiment per message
            entity.HasIndex(ms => ms.MessageId).IsUnique();

            // Create index on UpdatedAt for time-based queries
            entity.HasIndex(ms => ms.UpdatedAt);

            entity.HasOne(ms => ms.Message)
                .WithOne(m => m.MessageSentiment)
                .HasForeignKey<MessageSentiment>(ms => ms.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });


        // RAG search diagnostic entity configurations
        modelBuilder.Entity<RagSearchQuery>(entity =>
        {
            entity.HasKey(rsq => rsq.Id);
            entity.Property(rsq => rsq.Query).IsRequired();
            entity.Property(rsq => rsq.IndexName).IsRequired();
            entity.Property(rsq => rsq.CreatedAt).IsRequired();

            // Create index on CreatedAt for efficient time-based queries
            entity.HasIndex(rsq => rsq.CreatedAt);

            // Create index on RulesetId for filtering
            entity.HasIndex(rsq => rsq.RulesetId);
        });

        modelBuilder.Entity<RagSearchResultChunk>(entity =>
        {
            entity.HasKey(rsc => rsc.Id);
            entity.Property(rsc => rsc.RagSearchQueryId).IsRequired();
            entity.Property(rsc => rsc.ChunkId).IsRequired();
            entity.Property(rsc => rsc.DocumentId).IsRequired();
            entity.Property(rsc => rsc.DocumentName).IsRequired();
            entity.Property(rsc => rsc.EmbeddingId).IsRequired();
            entity.Property(rsc => rsc.RulesetId).IsRequired();
            entity.Property(rsc => rsc.Relevancy).IsRequired();

            entity.HasOne(rsc => rsc.RagSearchQuery)
                .WithMany(rsq => rsq.ResultChunks)
                .HasForeignKey(rsc => rsc.RagSearchQueryId)
                .OnDelete(DeleteBehavior.Cascade);

            // Create index on ChunkId for finding frequently matched chunks
            entity.HasIndex(rsc => rsc.ChunkId);

            // Create index on Relevancy for analysis queries
            entity.HasIndex(rsc => rsc.Relevancy);
        });

        modelBuilder.Entity<MessageFeedback>(entity =>
        {
            entity.HasKey(mf => mf.Id);
            entity.Property(mf => mf.MessageId).IsRequired();
            entity.Property(mf => mf.IsPositive).IsRequired();
            entity.Property(mf => mf.CreatedAt).IsRequired();

            entity.HasOne(mf => mf.Message)
                .WithMany()
                .HasForeignKey(mf => mf.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(mf => mf.InstructionVersion)
                .WithMany()
                .HasForeignKey(mf => mf.InstructionVersionId)
                .OnDelete(DeleteBehavior.NoAction);

            // Create unique index on MessageId to ensure one feedback per message
            entity.HasIndex(mf => mf.MessageId).IsUnique();

            // Create index on InstructionVersionId for tracking feedback by version
            entity.HasIndex(mf => mf.InstructionVersionId);
        });

        modelBuilder.Entity<MessageToolCall>(entity =>
        {
            entity.HasKey(mtc => mtc.Id);
            entity.Property(mtc => mtc.MessageId).IsRequired();
            entity.Property(mtc => mtc.ToolName).IsRequired().HasMaxLength(200);
            entity.Property(mtc => mtc.CreatedAt).IsRequired();

            entity.HasOne(mtc => mtc.Message)
                .WithMany(m => m.ToolCalls)
                .HasForeignKey(mtc => mtc.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(mtc => mtc.InstructionVersion)
                .WithMany()
                .HasForeignKey(mtc => mtc.InstructionVersionId)
                .OnDelete(DeleteBehavior.NoAction);

            // Create index on MessageId for efficient queries
            entity.HasIndex(mtc => mtc.MessageId);

            // Create index on InstructionVersionId for analytics
            entity.HasIndex(mtc => mtc.InstructionVersionId);

            entity.HasOne(mtc => mtc.Tool)
                .WithMany(t => t.ToolCalls)
                .HasForeignKey(mtc => mtc.ToolId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Tool>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).IsRequired().HasMaxLength(200);
            entity.HasIndex(t => t.Name).IsUnique();
        });

        modelBuilder.Entity<Evaluator>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Location tracking entity configurations
        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasKey(l => l.Id);
            entity.Property(l => l.GameId).IsRequired();
            entity.Property(l => l.Name).IsRequired().HasMaxLength(200);
            entity.Property(l => l.NameLower)
                .IsRequired()
                .HasMaxLength(200)
                .HasComputedColumnSql("LOWER(\"Name\")", stored: true);
            entity.Property(l => l.Description).IsRequired();
            entity.Property(l => l.CreatedAt).IsRequired();
            entity.Property(l => l.UpdatedAt).IsRequired();

            // Unique constraint: location names must be unique within a game (case-insensitive)
            // Using the NameLower computed column ensures the constraint is enforced at database level
            entity.HasIndex(l => new {l.GameId, l.NameLower}).IsUnique();

            entity.HasOne(l => l.Game)
                .WithMany()
                .HasForeignKey(l => l.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LocationEvent>(entity =>
        {
            entity.HasKey(le => le.Id);
            entity.Property(le => le.LocationId).IsRequired();
            entity.Property(le => le.EventName).IsRequired().HasMaxLength(200);
            entity.Property(le => le.EventDescription).IsRequired();
            entity.Property(le => le.CreatedAt).IsRequired();

            entity.HasOne(le => le.Location)
                .WithMany(l => l.Events)
                .HasForeignKey(le => le.LocationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NearbyLocation>(entity =>
        {
            entity.HasKey(nl => new {nl.SourceLocationId, nl.TargetLocationId});
            entity.Property(nl => nl.Distance).HasMaxLength(100);

            entity.HasOne(nl => nl.SourceLocation)
                .WithMany(l => l.NearbyLocationsAsSource)
                .HasForeignKey(nl => nl.SourceLocationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(nl => nl.TargetLocation)
                .WithMany(l => l.NearbyLocationsAsTarget)
                .HasForeignKey(nl => nl.TargetLocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Model entity configuration
        modelBuilder.Entity<Model>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Name).IsRequired().HasMaxLength(200);
            entity.Property(m => m.Provider).IsRequired().HasMaxLength(50);
            entity.Property(m => m.Endpoint).HasMaxLength(500);
            entity.Property(m => m.CreatedAt).IsRequired();

            // Create unique index on Name + Provider + Endpoint to prevent duplicates
            var indexBuilder = entity.HasIndex(m => new {m.Name, m.Provider, m.Endpoint})
                .IsUnique();

            // AreNullsDistinct is a relational-only feature. PostgreSQL 15+ supports it.
            // We only apply this if we're using a relational provider to avoid issues with In-Memory testing.
            if (Database.IsRelational())
            {
                indexBuilder.AreNullsDistinct(false);
            }
        });
        // Message evaluation metric entity configuration
        modelBuilder.Entity<MessageEvaluationMetric>(entity =>
        {
            entity.HasKey(mem => mem.Id);
            entity.Property(mem => mem.MessageId).IsRequired();
            entity.Property(mem => mem.MetricName).IsRequired();
            entity.Property(mem => mem.Score).IsRequired();
            entity.Property(mem => mem.EvaluatedAt).IsRequired();

            // Configure Diagnostics as JSONB for PostgreSQL
            string providerName;
            try
            {
                providerName = Database.ProviderName ?? "Npgsql.EntityFrameworkCore.PostgreSQL";
            }
            catch
            {
                // At design-time or when database is not initialized, assume PostgreSQL
                providerName = "Npgsql.EntityFrameworkCore.PostgreSQL";
            }

            if (string.Equals(providerName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
            {
                // For PostgreSQL, use jsonb type for Diagnostics
                entity.Property(mem => mem.Diagnostics)
                    .HasColumnType("jsonb");
            }

            // Create index on MessageId for efficient queries by message
            entity.HasIndex(mem => mem.MessageId);

            // Create index on EvaluatedAt for time-based queries
            entity.HasIndex(mem => mem.EvaluatedAt);

            // Create index on MetricName for filtering by metric type
            entity.HasIndex(mem => mem.MetricName);

            entity.HasOne(mem => mem.Message)
                .WithMany()
                .HasForeignKey(mem => mem.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(mem => mem.EvaluationModel)
                .WithMany(model => model.EvaluationMetrics)
                .HasForeignKey(mem => mem.EvaluationModelId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(mem => mem.Evaluator)
                .WithMany(e => e.EvaluationMetrics)
                .HasForeignKey(mem => mem.EvaluatorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EvaluationExecution>(entity =>
        {
            entity.HasKey(e => e.ExecutionName);
            entity.Property(e => e.ExecutionName).IsRequired().HasMaxLength(250);
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<EvaluationScenarioIteration>(entity =>
        {
            entity.HasKey(si => new {si.ExecutionName, si.ScenarioName, si.IterationName});
            entity.Property(si => si.ExecutionName).IsRequired().HasMaxLength(250);
            entity.Property(si => si.ScenarioName).IsRequired().HasMaxLength(250);
            entity.Property(si => si.IterationName).IsRequired().HasMaxLength(250);

            // Configure ResultJson as jsonb for PostgreSQL
            string providerName;
            try
            {
                providerName = Database.ProviderName ?? "Npgsql.EntityFrameworkCore.PostgreSQL";
            }
            catch
            {
                providerName = "Npgsql.EntityFrameworkCore.PostgreSQL";
            }

            if (string.Equals(providerName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
            {
                entity.Property(si => si.ResultJson).HasColumnType("jsonb");
            }

            entity.HasOne(si => si.Execution)
                .WithMany(e => e.ScenarioIterations)
                .HasForeignKey(si => si.ExecutionName)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Test case entity configurations
        modelBuilder.Entity<TestCase>(entity =>
        {
            entity.HasKey(tc => tc.Id);
            entity.Property(tc => tc.MessageId).IsRequired();
            entity.Property(tc => tc.Name).IsRequired().HasMaxLength(200);
            entity.Property(tc => tc.Description).HasMaxLength(2000);
            entity.Property(tc => tc.CreatedAt).IsRequired();
            entity.Property(tc => tc.IsActive).IsRequired();

            // Create unique index on MessageId to prevent duplicate test cases for same message
            entity.HasIndex(tc => tc.MessageId).IsUnique();

            entity.HasOne(tc => tc.Message)
                .WithOne(m => m.TestCase)
                .HasForeignKey<TestCase>(tc => tc.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TestCaseRun>(entity =>
        {
            entity.HasKey(tcr => tcr.Id);
            entity.Property(tcr => tcr.TestCaseId).IsRequired();
            entity.Property(tcr => tcr.AgentId).IsRequired();
            entity.Property(tcr => tcr.InstructionVersionId).IsRequired();
            entity.Property(tcr => tcr.ExecutedAt).IsRequired();
            entity.Property(tcr => tcr.GeneratedResponse).IsRequired();
            entity.Property(tcr => tcr.ExecutionName).HasMaxLength(250);

            // Create index on ExecutionName for report queries
            entity.HasIndex(tcr => tcr.ExecutionName);

            entity.HasOne(tcr => tcr.TestCase)
                .WithMany(tc => tc.Runs)
                .HasForeignKey(tcr => tcr.TestCaseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(tcr => tcr.Agent)
                .WithMany()
                .HasForeignKey(tcr => tcr.AgentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(tcr => tcr.InstructionVersion)
                .WithMany()
                .HasForeignKey(tcr => tcr.InstructionVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TestCaseRunMetric>(entity =>
        {
            entity.HasKey(tcrm => tcrm.Id);
            entity.Property(tcrm => tcrm.TestCaseRunId).IsRequired();
            entity.Property(tcrm => tcrm.MetricName).IsRequired().HasMaxLength(100);
            entity.Property(tcrm => tcrm.Score).IsRequired();
            entity.Property(tcrm => tcrm.Remarks).HasMaxLength(2000);

            // Create index on TestCaseRunId for efficient queries
            entity.HasIndex(tcrm => tcrm.TestCaseRunId);

            entity.HasOne(tcrm => tcrm.TestCaseRun)
                .WithMany(tcr => tcr.Metrics)
                .HasForeignKey(tcrm => tcrm.TestCaseRunId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(tcrm => tcrm.Evaluator)
                .WithMany()
                .HasForeignKey(tcrm => tcrm.EvaluatorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // StoredFile entity configuration
        modelBuilder.Entity<StoredFile>(entity =>
        {
            entity.HasKey(sf => sf.Id);
            entity.Property(sf => sf.ItemKind).IsRequired().HasMaxLength(50);
            entity.Property(sf => sf.FileName).IsRequired().HasMaxLength(255);
            entity.Property(sf => sf.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(sf => sf.CreatedAt).IsRequired();

            // Create index on ItemKind for efficient queries by type
            entity.HasIndex(sf => sf.ItemKind);
        });

        // Add relationship from TestCaseRun to StoredFile for reports
        modelBuilder.Entity<TestCaseRun>()
            .HasOne(tcr => tcr.ReportFile)
            .WithMany()
            .HasForeignKey(tcr => tcr.ReportFileId)
            .OnDelete(DeleteBehavior.SetNull);

        // Seed data - use lowercase ids for new defaults
        // IMPORTANT: When modifying any seed data values (e.g., ScenarioInstructions, InitialGreeting, or any HasData() values),
        // you MUST create a new EF Core migration. See AGENTS.md for the migration command.
        modelBuilder.Entity<Ruleset>()
            .HasData(
                new Ruleset {Id = "dnd5e", Name = "Dungeons and Dragons 5th Edition"}
            );

        modelBuilder.Entity<Player>()
            .HasData(
                new Player {Id = "emcee", RulesetId = "dnd5e", Description = "Default player", Name = "Emcee"},
                new Player
                {
                    Id = "kigorath", RulesetId = "dnd5e",
                    Description =
                        "A towering goliath druid who communes with the primal spirits of stone and storm. Their skin is marked with tribal patterns that glow faintly when channeling nature magic.",
                    Name = "Kigorath the Goliath Druid"
                },
                new Player
                {
                    Id = "glim", RulesetId = "dnd5e",
                    Description =
                        "A small frog wizard with bright, curious eyes and a penchant for collecting strange spell components. They speak in a croaky voice and are always eager to learn new magic.",
                    Name = "Glim the Frog Wizard"
                }
            );

        // Agents and instruction versions will be created programmatically during application startup
        // to avoid issues with auto-generated keys in EF migrations

        modelBuilder.Entity<Scenario>()
            .HasData(
                new Scenario
                {
                    Id = "islandTest",
                    RulesetId = "dnd5e",
                    Description = "Island test scenario",
                    Name = "Island Test",
                    ScenarioInstructions =
                        "This tropical island scenario features mysterious jungles, ancient ruins, and hidden dangers. The atmosphere is one of discovery and survival - players encounter exotic wildlife, tribal inhabitants, and forgotten treasures. Maintain a sense of wonder and exploration while keeping the tone adventurous and slightly perilous. The island has a rich history of shipwrecks, pirate legends, and ancient civilizations.",
                    InitialGreeting =
                        "You wake on a white sand beach, waves lapping at your boots. Jungle drums echo from the treeline ahead, and your scattered gear lies within reach. What do you do?"
                },
                new Scenario
                {
                    Id = "codemashKalahari",
                    RulesetId = "dnd5e",
                    Description = "A whimsical fantasy adventure set in a LEGO-themed conference center",
                    Name = "CodeMash Kalahari: The LEGO Realm",
                    ScenarioInstructions =
                        "The entire Kalahari Resort has been magically transformed into a LEGO-themed fantasy realm. Everything is made of colorful bricks - buildings, furniture, even the people are minifigures. Conference attendees have become adventurers on quests involving programming puzzles, LEGO building challenges, and whimsical brick-based combat. The tone is lighthearted and creative, with opportunities for clever problem-solving and LEGO-inspired humor. Players can build, rebuild, and explore this vibrant, colorful world.",
                    InitialGreeting =
                        "Welcome to CodeMash! The Kalahari Resort stretches before you, but something is wonderfully strange—everything is made of LEGO bricks. Colorful minifigures bustle past carrying tiny laptops, and the waterpark slides gleam in bright plastic hues. Tell me about your character: who are you and what kind of adventure do you seek?"
                }
            );
    }

    /// <summary>
    /// Converts a Vector to a byte array for storage in non-PostgreSQL databases.
    /// </summary>
    private static byte[]? ConvertVectorToBytes(Vector? vector)
    {
#pragma warning disable CS8604 // Possible null reference argument - Vector? is a nullable struct, null check is valid
        if (vector == null) return null;
#pragma warning restore CS8604

        // Get the float array from the vector
        float[] values = vector.ToArray();

        // Convert float array to byte array
        byte[] bytes = new byte[values.Length * sizeof(float)];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);

        return bytes;
    }

    /// <summary>
    /// Converts a byte array back to a Vector for non-PostgreSQL databases.
    /// </summary>
    private static Vector? ConvertBytesToVector(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0) return null;

        // Convert byte array back to float array
        int floatCount = bytes.Length / sizeof(float);
        float[] values = new float[floatCount];
        Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);

        return new Vector(values);
    }
}
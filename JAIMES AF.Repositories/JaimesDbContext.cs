namespace MattEland.Jaimes.Repositories;

public class JaimesDbContext(DbContextOptions<JaimesDbContext> options) : DbContext(options)
{
    public DbSet<Game> Games { get; set; } = null!;
    public DbSet<Message> Messages { get; set; } = null!;
    public DbSet<MessageEmbedding> MessageEmbeddings { get; set; } = null!;
    public DbSet<Player> Players { get; set; } = null!;
    public DbSet<Scenario> Scenarios { get; set; } = null!;
    public DbSet<Ruleset> Rulesets { get; set; } = null!;
    public DbSet<ChatHistory> ChatHistories { get; set; } = null!;
    
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

            entity.HasIndex(iv => new { iv.AgentId, iv.VersionNumber }).IsUnique();
        });

        modelBuilder.Entity<ScenarioAgent>(entity =>
        {
            entity.HasKey(sa => new { sa.ScenarioId, sa.AgentId });
            entity.Property(sa => sa.ScenarioId).IsRequired();
            entity.Property(sa => sa.AgentId).IsRequired();
            entity.Property(sa => sa.InstructionVersionId).IsRequired();

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

        // Seed data - use lowercase ids for new defaults
        // IMPORTANT: When modifying any seed data values (e.g., ScenarioInstructions, InitialGreeting, or any HasData() values),
        // you MUST create a new EF Core migration. See AGENTS.md for the migration command.
        modelBuilder.Entity<Ruleset>()
            .HasData(
                new Ruleset { Id = "dnd5e", Name = "Dungeons and Dragons 5th Edition" }
            );

        modelBuilder.Entity<Player>()
            .HasData(
                new Player { Id = "emcee", RulesetId = "dnd5e", Description = "Default player", Name = "Emcee" },
                new Player { Id = "kigorath", RulesetId = "dnd5e", Description = "A towering goliath druid who communes with the primal spirits of stone and storm. Their skin is marked with tribal patterns that glow faintly when channeling nature magic.", Name = "Kigorath the Goliath Druid" },
                new Player { Id = "glim", RulesetId = "dnd5e", Description = "A small frog wizard with bright, curious eyes and a penchant for collecting strange spell components. They speak in a croaky voice and are always eager to learn new magic.", Name = "Glim the Frog Wizard" }
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
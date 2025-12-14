namespace MattEland.Jaimes.Repositories;

public class JaimesDbContext(DbContextOptions<JaimesDbContext> options) : DbContext(options)
{
    public DbSet<Game> Games { get; set; } = null!;
    public DbSet<Message> Messages { get; set; } = null!;
    public DbSet<Player> Players { get; set; } = null!;
    public DbSet<Scenario> Scenarios { get; set; } = null!;
    public DbSet<Ruleset> Rulesets { get; set; } = null!;
    public DbSet<ChatHistory> ChatHistories { get; set; } = null!;

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
            entity.Property(s => s.SystemPrompt).IsRequired().HasDefaultValue("UPDATE ME");

            entity.HasOne(s => s.Ruleset)
                .WithMany(r => r.Scenarios)
                .HasForeignKey(s => s.RulesetId)
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
        // IMPORTANT: When modifying any seed data values (e.g., SystemPrompt, InitialGreeting, or any HasData() values),
        // you MUST create a new EF Core migration. See AGENTS.md for the migration command.
        modelBuilder.Entity<Ruleset>()
            .HasData(
                new Ruleset { Id = "dnd5e", Name = "Dungeons and Dragons 5th Edition" }
            );

        modelBuilder.Entity<Player>()
            .HasData(
                new Player { Id = "emcee", RulesetId = "dnd5e", Description = "Default player", Name = "Emcee" }
            );

        modelBuilder.Entity<Scenario>()
            .HasData(
                new Scenario
                {
                    Id = "islandTest",
                    RulesetId = "dnd5e",
                    Description = "Island test scenario",
                    Name = "Island Test",
                    SystemPrompt =
                        "You are a Dungeon Master running a solo D&D 5th Edition adventure. You guide a single player through an engaging narrative on a mysterious tropical island. Create vivid descriptions, present interesting choices, and adapt the story based on the player's actions. Use D&D 5e rules for combat, skill checks, and character interactions. Keep the adventure challenging but fair, and always maintain an immersive atmosphere. When formatting your responses, you may use markdown for bold (**text**) and italic (*text*) formatting, but do not use markdown headers (lines starting with #). Write naturally in paragraph form without section headers.",
                    InitialGreeting =
                        "You find yourself washed ashore on a pristine tropical beach. The warm sun beats down on your skin as you take in your surroundings. Crystal-clear turquoise water laps gently at the white sand beach. Behind you, a dense jungle stretches inland, filled with the sounds of exotic birds and rustling leaves. Your gear is scattered nearby, having survived the shipwreck that brought you here. You have no memory of how you arrived, but you know one thing: you must survive and discover the secrets of this mysterious island. What do you do first?"
                },
                new Scenario
                {
                    Id = "codemashKalahari",
                    RulesetId = "dnd5e",
                    Description = "A dark fantasy adventure set in a LEGO-themed cursed conference center",
                    Name = "CodeMash Kalahari: The Cursed Conference",
                    SystemPrompt =
                        "You are an AI dungeon master running a solo game of Dungeons and Dragons 5th Edition for a single human player controlling one character. Your goal is to keep the player challenged, advance the story, and create interesting encounters using rules consistent with D&D 5th Edition. This adventure takes place in early January 2026 at the Kalahari Resort conference center in Sandusky, Ohio, which has been cursed by a dark lich. The entire area is surrounded by blizzards and fell magic that has transformed everything into a LEGO-themed fantasy realm. Take inspiration from LEGO sets, themes, and concepts when describing the world, its inhabitants, architecture, and creatures. Maintain a fantasy technology level appropriate for D&D, but treat the player as someone who has traveled from the modern world into this twisted fantasy realm. The player should feel like they've stepped from reality into a nightmarish LEGO world where the familiar has become fantastical and dangerous. When formatting your responses, you may use markdown for bold (**text**) and italic (*text*) formatting, but do not use markdown headers (lines starting with #). Write naturally in paragraph form without section headers.",
                    InitialGreeting =
                        "Hello, adventurer. Welcome to what was once the CodeMash conference at the Kalahari Resort—though you'll find it's become something far stranger and more dangerous. The blizzards outside rage endlessly, and the fell magic of a dark lich has twisted this place into a realm where everything is made of bricks, where the familiar has become fantastical, and where every corner could hold both wonder and peril. Before we begin our journey through this twisted and demented brick world, tell me about your character. Who are you? What brings you here? And what kind of adventure are you seeking—do you crave mystery, combat, exploration, or something else entirely? Once I know more about you, I'll set the scene and we'll begin our tale."
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
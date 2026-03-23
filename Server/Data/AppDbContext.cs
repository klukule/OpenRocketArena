using Microsoft.EntityFrameworkCore;
using OpenRocketArena.Server.Entities;

namespace OpenRocketArena.Server.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Persona> Personas => Set<Persona>();
    public DbSet<OAuthSession> OAuthSessions => Set<OAuthSession>();
    public DbSet<Quest> Quests => Set<Quest>();
    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();
    public DbSet<CharacterProgression> CharacterProgressions => Set<CharacterProgression>();
    public DbSet<EquipmentSet> EquipmentSets => Set<EquipmentSet>();
    public DbSet<EquipmentSetItem> EquipmentSetItems => Set<EquipmentSetItem>();
    public DbSet<CharacterEmote> CharacterEmotes => Set<CharacterEmote>();
    public DbSet<ProfileEquipItem> ProfileEquipItems => Set<ProfileEquipItem>();
    public DbSet<ItemLevel> ItemLevels => Set<ItemLevel>();
    public DbSet<PlaylistRanking> PlaylistRankings => Set<PlaylistRanking>();
    public DbSet<BlastPassProgression> BlastPassProgressions => Set<BlastPassProgression>();
    public DbSet<MotdView> MotdViews => Set<MotdView>();
    public DbSet<PlayerInventory> PlayerInventories => Set<PlayerInventory>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryChest> InventoryChests => Set<InventoryChest>();
    public DbSet<InventoryDuplicateItem> InventoryDuplicateItems => Set<InventoryDuplicateItem>();
    public DbSet<InventoryBlastPass> InventoryBlastPasses => Set<InventoryBlastPass>();
    public DbSet<InventoryPromotion> InventoryPromotions => Set<InventoryPromotion>();
    public DbSet<InventoryOneTimeOffer> InventoryOneTimeOffers => Set<InventoryOneTimeOffer>();
    public DbSet<IamSession> IamSessions => Set<IamSession>();
    public DbSet<MatchHistory> MatchHistories => Set<MatchHistory>();
    public DbSet<PlayerStatGroup> PlayerStatGroups => Set<PlayerStatGroup>();
    public DbSet<PlayerStatEntry> PlayerStatEntries => Set<PlayerStatEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(e =>
        {
            e.HasIndex(a => a.SteamId).IsUnique();
            e.HasMany(a => a.Personas).WithOne(p => p.Account).HasForeignKey(p => p.AccountId);
            e.HasMany(a => a.Sessions).WithOne(s => s.Account).HasForeignKey(s => s.AccountId);
            e.HasMany(a => a.Quests).WithOne(q => q.Account).HasForeignKey(q => q.AccountId);
            e.HasMany(a => a.IamSessions).WithOne(s => s.Account).HasForeignKey(s => s.AccountId);
            e.HasMany(a => a.MatchHistories).WithOne(m => m.Account).HasForeignKey(m => m.AccountId);
        });

        modelBuilder.Entity<PlayerProfile>(e =>
        {
            e.HasOne(p => p.Account).WithOne(a => a.Profile).HasForeignKey<PlayerProfile>(p => p.AccountId);
            e.HasIndex(p => p.AccountId).IsUnique();
            e.HasMany(p => p.CharacterProgressions).WithOne(c => c.Profile).HasForeignKey(c => c.ProfileId);
            e.HasMany(p => p.Equipment).WithOne(eq => eq.Profile).HasForeignKey(eq => eq.ProfileId);
            e.HasMany(p => p.ItemLevels).WithOne(il => il.Profile).HasForeignKey(il => il.ProfileId);
            e.HasMany(p => p.PlaylistRankings).WithOne(pr => pr.Profile).HasForeignKey(pr => pr.ProfileId);
            e.HasMany(p => p.BlastPassLevels).WithOne(bp => bp.Profile).HasForeignKey(bp => bp.ProfileId);
            e.HasMany(p => p.MotdViews).WithOne(m => m.Profile).HasForeignKey(m => m.ProfileId);
            e.HasMany(p => p.StatGroups).WithOne(sg => sg.Profile).HasForeignKey(sg => sg.ProfileId);
        });

        modelBuilder.Entity<PlayerStatGroup>(e =>
        {
            e.HasMany(sg => sg.Stats).WithOne(s => s.StatGroup).HasForeignKey(s => s.StatGroupId);
            e.HasIndex(sg => new { sg.ProfileId, sg.SliceType, sg.SliceValue }).IsUnique();
        });

        modelBuilder.Entity<PlayerStatEntry>(e =>
        {
            e.HasIndex(s => new { s.StatGroupId, s.Metric });
        });

        modelBuilder.Entity<CharacterProgression>(e =>
        {
            e.HasMany(c => c.EquipmentSets).WithOne(es => es.CharacterProgression).HasForeignKey(es => es.CharacterProgressionId);
            e.HasMany(c => c.Emotes).WithOne(em => em.CharacterProgression).HasForeignKey(em => em.CharacterProgressionId);
        });

        modelBuilder.Entity<EquipmentSet>(e =>
        {
            e.HasMany(es => es.Items).WithOne(i => i.EquipmentSet).HasForeignKey(i => i.EquipmentSetId);
        });

        modelBuilder.Entity<PlayerInventory>(e =>
        {
            e.HasOne(i => i.Account).WithOne(a => a.Inventory).HasForeignKey<PlayerInventory>(i => i.AccountId);
            e.HasIndex(i => i.AccountId).IsUnique();
            e.HasMany(i => i.Items).WithOne(it => it.Inventory).HasForeignKey(it => it.InventoryId);
            e.HasMany(i => i.Chests).WithOne(c => c.Inventory).HasForeignKey(c => c.InventoryId);
            e.HasMany(i => i.DupeItems).WithOne(d => d.Inventory).HasForeignKey(d => d.InventoryId);
            e.HasMany(i => i.BlastPasses).WithOne(b => b.Inventory).HasForeignKey(b => b.InventoryId);
            e.HasMany(i => i.Promotions).WithOne(p => p.Inventory).HasForeignKey(p => p.InventoryId);
            e.HasMany(i => i.OneTimeOffers).WithOne(o => o.Inventory).HasForeignKey(o => o.InventoryId);
        });
    }
}

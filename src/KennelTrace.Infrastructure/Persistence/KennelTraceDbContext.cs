using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Animals;
using KennelTrace.Domain.Features.Facilities;
using KennelTrace.Domain.Features.Imports;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Domain.Features.Tracing;
using Microsoft.EntityFrameworkCore;

namespace KennelTrace.Infrastructure.Persistence;

public sealed class KennelTraceDbContext(DbContextOptions<KennelTraceDbContext> options) : DbContext(options)
{
    public DbSet<Facility> Facilities => Set<Facility>();

    public DbSet<Location> Locations => Set<Location>();

    public DbSet<LocationLink> LocationLinks => Set<LocationLink>();

    public DbSet<Animal> Animals => Set<Animal>();

    public DbSet<MovementEvent> MovementEvents => Set<MovementEvent>();

    public DbSet<Disease> Diseases => Set<Disease>();

    public DbSet<DiseaseTraceProfile> DiseaseTraceProfiles => Set<DiseaseTraceProfile>();

    public DbSet<DiseaseTraceProfileTopologyLinkType> DiseaseTraceProfileTopologyLinkTypes => Set<DiseaseTraceProfileTopologyLinkType>();

    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    public DbSet<ImportIssue> ImportIssues => Set<ImportIssue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureFacilities(modelBuilder);
        ConfigureLocations(modelBuilder);
        ConfigureLocationLinks(modelBuilder);
        ConfigureAnimals(modelBuilder);
        ConfigureMovementEvents(modelBuilder);
        ConfigureDiseases(modelBuilder);
        ConfigureDiseaseTraceProfiles(modelBuilder);
        ConfigureImports(modelBuilder);
    }

    private static void ConfigureFacilities(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Facility>();

        entity.ToTable("Facilities");
        entity.HasKey(x => x.FacilityId);
        entity.Property(x => x.FacilityId).UseIdentityColumn();
        entity.Property(x => x.FacilityCode).HasConversion(x => x.Value, x => new FacilityCode(x)).HasMaxLength(50).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
        entity.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
        entity.Property(x => x.IsActive).HasDefaultValue(true);
        entity.Property(x => x.Notes).HasMaxLength(500);
        entity.Property(x => x.CreatedUtc).HasColumnType("datetime2").IsRequired();
        entity.Property(x => x.ModifiedUtc).HasColumnType("datetime2").IsRequired();
        entity.HasIndex(x => x.FacilityCode).IsUnique().HasDatabaseName("UQ_Facilities_FacilityCode");
    }

    private static void ConfigureLocations(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Location>();

        entity.ToTable("Locations", table =>
        {
            table.HasCheckConstraint("CK_Locations_NotOwnParent", "[ParentLocationId] IS NULL OR [ParentLocationId] <> [LocationId]");
            table.HasCheckConstraint("CK_Locations_KennelHasParent", "[LocationType] <> N'Kennel' OR [ParentLocationId] IS NOT NULL");
            table.HasCheckConstraint("CK_Locations_LocationType", "[LocationType] IN (N'Room', N'Hallway', N'Medical', N'Isolation', N'Intake', N'Yard', N'Kennel', N'Other')");
            table.HasCheckConstraint("CK_Locations_GridRow", "[GridRow] IS NULL OR [GridRow] >= 0");
            table.HasCheckConstraint("CK_Locations_GridColumn", "[GridColumn] IS NULL OR [GridColumn] >= 0");
            table.HasCheckConstraint("CK_Locations_StackLevel", "[StackLevel] >= 0");
            table.HasCheckConstraint("CK_Locations_GridCoordinatesTogether", "([GridRow] IS NULL AND [GridColumn] IS NULL) OR ([GridRow] IS NOT NULL AND [GridColumn] IS NOT NULL)");
        });

        entity.HasKey(x => x.LocationId);
        entity.Property(x => x.LocationId).UseIdentityColumn();
        entity.Property(x => x.FacilityId).IsRequired();
        entity.Property(x => x.ParentLocationId);
        entity.Property(x => x.LocationType).HasConversion<string>().HasMaxLength(30).IsRequired();
        entity.Property(x => x.LocationCode).HasConversion(x => x.Value, x => new LocationCode(x)).HasMaxLength(50).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
        entity.Property(x => x.GridRow);
        entity.Property(x => x.GridColumn);
        entity.Property(x => x.StackLevel).HasDefaultValue(0).IsRequired();
        entity.Property(x => x.DisplayOrder);
        entity.Property(x => x.IsActive).HasDefaultValue(true);
        entity.Property(x => x.Notes).HasMaxLength(500);
        entity.Property(x => x.CreatedUtc).HasColumnType("datetime2").IsRequired();
        entity.Property(x => x.ModifiedUtc).HasColumnType("datetime2").IsRequired();

        entity.HasAlternateKey(x => new { x.LocationId, x.FacilityId }).HasName("UQ_Locations_LocationId_Facility");
        entity.HasIndex(x => new { x.FacilityId, x.LocationCode }).IsUnique().HasDatabaseName("UQ_Locations_Facility_LocationCode");

        entity.HasOne<Facility>()
            .WithMany()
            .HasForeignKey(x => x.FacilityId)
            .HasConstraintName("FK_Locations_Facilities")
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.ParentLocation)
            .WithMany(x => (IEnumerable<Location>)x.Children)
            .HasForeignKey(x => new { x.ParentLocationId, x.FacilityId })
            .HasPrincipalKey(x => new { x.LocationId, x.FacilityId })
            .HasConstraintName("FK_Locations_ParentSameFacility")
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => new { x.ParentLocationId, x.GridRow, x.GridColumn, x.StackLevel })
            .IsUnique()
            .HasDatabaseName("UX_Locations_ActiveKennelGridPosition")
            .HasFilter("[LocationType] = N'Kennel' AND [IsActive] = 1 AND [ParentLocationId] IS NOT NULL AND [GridRow] IS NOT NULL AND [GridColumn] IS NOT NULL");

        entity.HasIndex(x => new { x.FacilityId, x.ParentLocationId, x.IsActive, x.LocationType })
            .HasDatabaseName("IX_Locations_Facility_Parent");
        entity.HasIndex(x => new { x.FacilityId, x.LocationType, x.IsActive })
            .HasDatabaseName("IX_Locations_Facility_Type_Active");
        entity.HasIndex(x => new { x.ParentLocationId, x.DisplayOrder, x.Name })
            .HasDatabaseName("IX_Locations_Parent_DisplayOrder");
    }

    private static void ConfigureLocationLinks(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<LocationLink>();

        entity.ToTable("LocationLinks", table =>
        {
            table.HasCheckConstraint("CK_LocationLinks_NotSelf", "[FromLocationId] <> [ToLocationId]");
            table.HasCheckConstraint("CK_LocationLinks_LinkType", "[LinkType] IN (N'AdjacentLeft', N'AdjacentRight', N'AdjacentAbove', N'AdjacentBelow', N'AdjacentOther', N'Connected', N'Airflow', N'TransportPath')");
            table.HasCheckConstraint("CK_LocationLinks_SourceType", "[SourceType] IN (N'Manual', N'Import', N'Derived')");
        });

        entity.HasKey(x => x.LocationLinkId);
        entity.Property(x => x.LocationLinkId).UseIdentityColumn();
        entity.Property(x => x.FacilityId).IsRequired();
        entity.Property(x => x.FromLocationId).IsRequired();
        entity.Property(x => x.ToLocationId).IsRequired();
        entity.Property(x => x.LinkType).HasConversion<string>().HasMaxLength(30).IsRequired();
        entity.Property(x => x.IsActive).HasDefaultValue(true);
        entity.Property(x => x.SourceType).HasConversion<string>().HasMaxLength(20).HasDefaultValue(SourceType.Manual).IsRequired();
        entity.Property(x => x.SourceReference).HasMaxLength(200);
        entity.Property(x => x.Notes).HasMaxLength(500);
        entity.Property(x => x.CreatedUtc).HasColumnType("datetime2").IsRequired();
        entity.Property(x => x.ModifiedUtc).HasColumnType("datetime2").IsRequired();

        entity.HasOne<Facility>()
            .WithMany()
            .HasForeignKey(x => x.FacilityId)
            .HasConstraintName("FK_LocationLinks_Facilities")
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<Location>()
            .WithMany()
            .HasForeignKey(x => new { x.FromLocationId, x.FacilityId })
            .HasPrincipalKey(x => new { x.LocationId, x.FacilityId })
            .HasConstraintName("FK_LocationLinks_FromLocationSameFacility")
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<Location>()
            .WithMany()
            .HasForeignKey(x => new { x.ToLocationId, x.FacilityId })
            .HasPrincipalKey(x => new { x.LocationId, x.FacilityId })
            .HasConstraintName("FK_LocationLinks_ToLocationSameFacility")
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => new { x.FromLocationId, x.ToLocationId, x.LinkType })
            .IsUnique()
            .HasDatabaseName("UX_LocationLinks_ActiveDirected")
            .HasFilter("[IsActive] = 1");

        entity.HasIndex(x => new { x.FacilityId, x.LinkType, x.IsActive }).HasDatabaseName("IX_LocationLinks_Facility_Type_Active");
        entity.HasIndex(x => new { x.FromLocationId, x.LinkType, x.IsActive }).HasDatabaseName("IX_LocationLinks_From_Type_Active");
        entity.HasIndex(x => new { x.ToLocationId, x.LinkType, x.IsActive }).HasDatabaseName("IX_LocationLinks_To_Type_Active");
    }

    private static void ConfigureAnimals(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Animal>();

        entity.ToTable("Animals");
        entity.HasKey(x => x.AnimalId);
        entity.Property(x => x.AnimalId).UseIdentityColumn();
        entity.Property(x => x.AnimalNumber).HasConversion(x => x.Value, x => new AnimalCode(x)).HasMaxLength(50).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(100);
        entity.Property(x => x.Species).HasMaxLength(20).HasDefaultValue("Dog").IsRequired();
        entity.Property(x => x.Sex).HasMaxLength(20);
        entity.Property(x => x.Breed).HasMaxLength(100);
        entity.Property(x => x.DateOfBirth).HasColumnType("date");
        entity.Property(x => x.IsActive).HasDefaultValue(true);
        entity.Property(x => x.Notes).HasMaxLength(500);
        entity.Property(x => x.CreatedUtc).HasColumnType("datetime2").IsRequired();
        entity.Property(x => x.ModifiedUtc).HasColumnType("datetime2").IsRequired();
        entity.HasIndex(x => x.AnimalNumber).IsUnique().HasDatabaseName("UQ_Animals_AnimalNumber");
    }

    private static void ConfigureMovementEvents(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<MovementEvent>();

        entity.ToTable("MovementEvents", table =>
        {
            table.HasCheckConstraint("CK_MovementEvents_EndAfterStart", "[EndUtc] IS NULL OR [EndUtc] > [StartUtc]");
            table.HasCheckConstraint("CK_MovementEvents_SourceType", "[SourceType] IN (N'Manual', N'Import', N'Derived')");
        });

        entity.HasKey(x => x.MovementEventId);
        entity.Property(x => x.MovementEventId).UseIdentityColumn();
        entity.Property(x => x.AnimalId).IsRequired();
        entity.Property(x => x.LocationId).IsRequired();
        entity.Property(x => x.StartUtc).HasColumnType("datetime2").IsRequired();
        entity.Property(x => x.EndUtc).HasColumnType("datetime2");
        entity.Property(x => x.MovementReason).HasMaxLength(50);
        entity.Property(x => x.SourceType).HasConversion<string>().HasMaxLength(20).HasDefaultValue(SourceType.Manual).IsRequired();
        entity.Property(x => x.RecordedByUserId).HasMaxLength(450);
        entity.Property(x => x.Notes).HasMaxLength(500);
        entity.Property(x => x.CreatedUtc).HasColumnType("datetime2").IsRequired();
        entity.Property(x => x.ModifiedUtc).HasColumnType("datetime2").IsRequired();

        entity.HasOne<Animal>()
            .WithMany()
            .HasForeignKey(x => x.AnimalId)
            .HasConstraintName("FK_MovementEvents_Animals")
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<Location>()
            .WithMany()
            .HasForeignKey(x => x.LocationId)
            .HasConstraintName("FK_MovementEvents_Locations")
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => x.AnimalId)
            .IsUnique()
            .HasDatabaseName("UX_MovementEvents_OneOpenStayPerAnimal")
            .HasFilter("[EndUtc] IS NULL");

        entity.HasIndex(x => new { x.AnimalId, x.StartUtc, x.EndUtc, x.LocationId }).HasDatabaseName("IX_MovementEvents_Animal_Start_End").IsDescending(true, false, false, false);
        entity.HasIndex(x => new { x.LocationId, x.StartUtc, x.EndUtc, x.AnimalId }).HasDatabaseName("IX_MovementEvents_Location_Start_End");
        entity.HasIndex(x => new { x.LocationId, x.AnimalId }).HasDatabaseName("IX_MovementEvents_OpenByLocation").HasFilter("[EndUtc] IS NULL");
    }

    private static void ConfigureDiseases(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Disease>();

        entity.ToTable("Diseases");
        entity.HasKey(x => x.DiseaseId);
        entity.Property(x => x.DiseaseId).UseIdentityColumn();
        entity.Property(x => x.DiseaseCode).HasConversion(x => x.Value, x => new DiseaseCode(x)).HasMaxLength(50).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
        entity.Property(x => x.IsActive).HasDefaultValue(true);
        entity.Property(x => x.Notes).HasMaxLength(500);
        entity.Property(x => x.CreatedUtc).HasColumnType("datetime2").IsRequired();
        entity.Property(x => x.ModifiedUtc).HasColumnType("datetime2").IsRequired();
        entity.HasIndex(x => x.DiseaseCode).IsUnique().HasDatabaseName("UQ_Diseases_DiseaseCode");
    }

    private static void ConfigureDiseaseTraceProfiles(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<DiseaseTraceProfile>();

        entity.ToTable("DiseaseTraceProfiles", table =>
        {
            table.HasCheckConstraint("CK_DiseaseTraceProfiles_Lookback", "[DefaultLookbackHours] > 0");
            table.HasCheckConstraint("CK_DiseaseTraceProfiles_AdjacencySettings", "([IncludeAdjacent] = 0 AND [AdjacencyDepth] = 0) OR ([IncludeAdjacent] = 1 AND [AdjacencyDepth] > 0)");
            table.HasCheckConstraint("CK_DiseaseTraceProfiles_TopologySettings", "([IncludeTopologyLinks] = 0 AND [TopologyDepth] = 0) OR ([IncludeTopologyLinks] = 1 AND [TopologyDepth] > 0)");
        });

        entity.HasKey(x => x.DiseaseTraceProfileId);
        entity.Property(x => x.DiseaseTraceProfileId).UseIdentityColumn();
        entity.Property(x => x.DiseaseId).IsRequired();
        entity.Property(x => x.DefaultLookbackHours).IsRequired();
        entity.Property(x => x.IncludeSameLocation).HasDefaultValue(true).IsRequired();
        entity.Property(x => x.IncludeSameRoom).HasDefaultValue(true).IsRequired();
        entity.Property(x => x.IncludeAdjacent).HasDefaultValue(true).IsRequired();
        entity.Property(x => x.AdjacencyDepth).HasDefaultValue(1).IsRequired();
        entity.Property(x => x.IncludeTopologyLinks).HasDefaultValue(false).IsRequired();
        entity.Property(x => x.TopologyDepth).HasDefaultValue(0).IsRequired();
        entity.Property(x => x.IsActive).HasDefaultValue(true).IsRequired();
        entity.Property(x => x.Notes).HasMaxLength(500);
        entity.Property(x => x.CreatedUtc).HasColumnType("datetime2").IsRequired();
        entity.Property(x => x.ModifiedUtc).HasColumnType("datetime2").IsRequired();

        entity.HasOne<Disease>()
            .WithMany()
            .HasForeignKey(x => x.DiseaseId)
            .HasConstraintName("FK_DiseaseTraceProfiles_Diseases")
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => x.DiseaseId).IsUnique().HasDatabaseName("UQ_DiseaseTraceProfiles_Disease");
        entity.HasIndex(x => new { x.DiseaseId, x.IsActive }).HasDatabaseName("IX_DiseaseTraceProfiles_Disease_Active");

        entity.HasMany(x => x.TopologyLinkTypes)
            .WithOne()
            .HasForeignKey(x => x.DiseaseTraceProfileId)
            .HasConstraintName("FK_DiseaseTraceProfileTopologyLinkTypes_Profile")
            .OnDelete(DeleteBehavior.Cascade);

        var topologyEntity = modelBuilder.Entity<DiseaseTraceProfileTopologyLinkType>();
        topologyEntity.ToTable("DiseaseTraceProfileTopologyLinkTypes", table =>
        {
            table.HasCheckConstraint("CK_DiseaseTraceProfileTopologyLinkTypes_LinkType", "[LinkType] IN (N'Connected', N'Airflow', N'TransportPath')");
        });
        topologyEntity.HasKey(x => new { x.DiseaseTraceProfileId, x.LinkType }).HasName("PK_DiseaseTraceProfileTopologyLinkTypes");
        topologyEntity.Property(x => x.LinkType).HasConversion<string>().HasMaxLength(30).IsRequired();
    }

    private static void ConfigureImports(ModelBuilder modelBuilder)
    {
        var batchEntity = modelBuilder.Entity<ImportBatch>();

        batchEntity.ToTable("ImportBatches", table =>
        {
            table.HasCheckConstraint("CK_ImportBatches_RunMode", "[RunMode] IN (N'ValidateOnly', N'Commit')");
            table.HasCheckConstraint("CK_ImportBatches_Status", "[Status] IN (N'Pending', N'Failed', N'Succeeded')");
        });

        batchEntity.HasKey(x => x.ImportBatchId);
        batchEntity.Property(x => x.ImportBatchId).UseIdentityColumn();
        batchEntity.Property(x => x.BatchType).HasMaxLength(50).IsRequired();
        batchEntity.Property(x => x.FacilityId);
        batchEntity.Property(x => x.SourceFileName).HasMaxLength(260).IsRequired();
        batchEntity.Property(x => x.SourceFileHash).HasMaxLength(128);
        batchEntity.Property(x => x.RunMode).HasConversion<string>().HasMaxLength(20).IsRequired();
        batchEntity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        batchEntity.Property(x => x.StartedUtc).HasColumnType("datetime2").IsRequired();
        batchEntity.Property(x => x.CompletedUtc).HasColumnType("datetime2");
        batchEntity.Property(x => x.ExecutedByUserId).HasMaxLength(450);
        batchEntity.Property(x => x.Summary).HasColumnType("nvarchar(max)");

        batchEntity.HasOne<Facility>()
            .WithMany()
            .HasForeignKey(x => x.FacilityId)
            .HasConstraintName("FK_ImportBatches_Facilities")
            .OnDelete(DeleteBehavior.Restrict);

        var issueEntity = modelBuilder.Entity<ImportIssue>();
        issueEntity.ToTable("ImportIssues", table =>
        {
            table.HasCheckConstraint("CK_ImportIssues_Severity", "[Severity] IN (N'Error', N'Warning')");
        });
        issueEntity.HasKey(x => x.ImportIssueId);
        issueEntity.Property(x => x.ImportIssueId).UseIdentityColumn();
        issueEntity.Property(x => x.ImportBatchId).IsRequired();
        issueEntity.Property(x => x.Severity).HasConversion<string>().HasMaxLength(10).IsRequired();
        issueEntity.Property(x => x.SheetName).HasMaxLength(100).IsRequired();
        issueEntity.Property(x => x.RowNumber);
        issueEntity.Property(x => x.ItemKey).HasMaxLength(200);
        issueEntity.Property(x => x.Message).HasMaxLength(1000).IsRequired();
        issueEntity.HasOne<ImportBatch>()
            .WithMany()
            .HasForeignKey(x => x.ImportBatchId)
            .HasConstraintName("FK_ImportIssues_ImportBatches")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

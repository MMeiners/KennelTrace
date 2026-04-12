using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KennelTrace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlServerPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Animals",
                columns: table => new
                {
                    AnimalId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnimalNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Species = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Dog"),
                    Sex = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Breed = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Animals", x => x.AnimalId);
                });

            migrationBuilder.CreateTable(
                name: "Diseases",
                columns: table => new
                {
                    DiseaseId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiseaseCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Diseases", x => x.DiseaseId);
                });

            migrationBuilder.CreateTable(
                name: "Facilities",
                columns: table => new
                {
                    FacilityId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FacilityCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Facilities", x => x.FacilityId);
                });

            migrationBuilder.CreateTable(
                name: "DiseaseTraceProfiles",
                columns: table => new
                {
                    DiseaseTraceProfileId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiseaseId = table.Column<int>(type: "int", nullable: false),
                    DefaultLookbackHours = table.Column<int>(type: "int", nullable: false),
                    IncludeSameLocation = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IncludeSameRoom = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IncludeAdjacent = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    AdjacencyDepth = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    IncludeTopologyLinks = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    TopologyDepth = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiseaseTraceProfiles", x => x.DiseaseTraceProfileId);
                    table.CheckConstraint("CK_DiseaseTraceProfiles_AdjacencySettings", "([IncludeAdjacent] = 0 AND [AdjacencyDepth] = 0) OR ([IncludeAdjacent] = 1 AND [AdjacencyDepth] > 0)");
                    table.CheckConstraint("CK_DiseaseTraceProfiles_Lookback", "[DefaultLookbackHours] > 0");
                    table.CheckConstraint("CK_DiseaseTraceProfiles_TopologySettings", "([IncludeTopologyLinks] = 0 AND [TopologyDepth] = 0) OR ([IncludeTopologyLinks] = 1 AND [TopologyDepth] > 0)");
                    table.ForeignKey(
                        name: "FK_DiseaseTraceProfiles_Diseases",
                        column: x => x.DiseaseId,
                        principalTable: "Diseases",
                        principalColumn: "DiseaseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    ImportBatchId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FacilityId = table.Column<int>(type: "int", nullable: true),
                    SourceFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    SourceFileHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RunMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExecutedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.ImportBatchId);
                    table.CheckConstraint("CK_ImportBatches_RunMode", "[RunMode] IN (N'ValidateOnly', N'Commit')");
                    table.CheckConstraint("CK_ImportBatches_Status", "[Status] IN (N'Pending', N'Failed', N'Succeeded')");
                    table.ForeignKey(
                        name: "FK_ImportBatches_Facilities",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "FacilityId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    LocationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FacilityId = table.Column<int>(type: "int", nullable: false),
                    ParentLocationId = table.Column<int>(type: "int", nullable: true),
                    LocationType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    LocationCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GridRow = table.Column<int>(type: "int", nullable: true),
                    GridColumn = table.Column<int>(type: "int", nullable: true),
                    StackLevel = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    DisplayOrder = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.LocationId);
                    table.UniqueConstraint("UQ_Locations_LocationId_Facility", x => new { x.LocationId, x.FacilityId });
                    table.CheckConstraint("CK_Locations_GridColumn", "[GridColumn] IS NULL OR [GridColumn] >= 0");
                    table.CheckConstraint("CK_Locations_GridCoordinatesTogether", "([GridRow] IS NULL AND [GridColumn] IS NULL) OR ([GridRow] IS NOT NULL AND [GridColumn] IS NOT NULL)");
                    table.CheckConstraint("CK_Locations_GridRow", "[GridRow] IS NULL OR [GridRow] >= 0");
                    table.CheckConstraint("CK_Locations_KennelHasParent", "[LocationType] <> N'Kennel' OR [ParentLocationId] IS NOT NULL");
                    table.CheckConstraint("CK_Locations_LocationType", "[LocationType] IN (N'Room', N'Hallway', N'Medical', N'Isolation', N'Intake', N'Yard', N'Kennel', N'Other')");
                    table.CheckConstraint("CK_Locations_NotOwnParent", "[ParentLocationId] IS NULL OR [ParentLocationId] <> [LocationId]");
                    table.CheckConstraint("CK_Locations_StackLevel", "[StackLevel] >= 0");
                    table.ForeignKey(
                        name: "FK_Locations_Facilities",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "FacilityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Locations_ParentSameFacility",
                        columns: x => new { x.ParentLocationId, x.FacilityId },
                        principalTable: "Locations",
                        principalColumns: new[] { "LocationId", "FacilityId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiseaseTraceProfileTopologyLinkTypes",
                columns: table => new
                {
                    DiseaseTraceProfileId = table.Column<int>(type: "int", nullable: false),
                    LinkType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiseaseTraceProfileTopologyLinkTypes", x => new { x.DiseaseTraceProfileId, x.LinkType });
                    table.CheckConstraint("CK_DiseaseTraceProfileTopologyLinkTypes_LinkType", "[LinkType] IN (N'Connected', N'Airflow', N'TransportPath')");
                    table.ForeignKey(
                        name: "FK_DiseaseTraceProfileTopologyLinkTypes_Profile",
                        column: x => x.DiseaseTraceProfileId,
                        principalTable: "DiseaseTraceProfiles",
                        principalColumn: "DiseaseTraceProfileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportIssues",
                columns: table => new
                {
                    ImportIssueId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImportBatchId = table.Column<long>(type: "bigint", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SheetName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: true),
                    ItemKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportIssues", x => x.ImportIssueId);
                    table.CheckConstraint("CK_ImportIssues_Severity", "[Severity] IN (N'Error', N'Warning')");
                    table.ForeignKey(
                        name: "FK_ImportIssues_ImportBatches",
                        column: x => x.ImportBatchId,
                        principalTable: "ImportBatches",
                        principalColumn: "ImportBatchId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LocationLinks",
                columns: table => new
                {
                    LocationLinkId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FacilityId = table.Column<int>(type: "int", nullable: false),
                    FromLocationId = table.Column<int>(type: "int", nullable: false),
                    ToLocationId = table.Column<int>(type: "int", nullable: false),
                    LinkType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SourceType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Manual"),
                    SourceReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationLinks", x => x.LocationLinkId);
                    table.CheckConstraint("CK_LocationLinks_LinkType", "[LinkType] IN (N'AdjacentLeft', N'AdjacentRight', N'AdjacentAbove', N'AdjacentBelow', N'AdjacentOther', N'Connected', N'Airflow', N'TransportPath')");
                    table.CheckConstraint("CK_LocationLinks_NotSelf", "[FromLocationId] <> [ToLocationId]");
                    table.CheckConstraint("CK_LocationLinks_SourceType", "[SourceType] IN (N'Manual', N'Import', N'Derived')");
                    table.ForeignKey(
                        name: "FK_LocationLinks_Facilities",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "FacilityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LocationLinks_FromLocationSameFacility",
                        columns: x => new { x.FromLocationId, x.FacilityId },
                        principalTable: "Locations",
                        principalColumns: new[] { "LocationId", "FacilityId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LocationLinks_ToLocationSameFacility",
                        columns: x => new { x.ToLocationId, x.FacilityId },
                        principalTable: "Locations",
                        principalColumns: new[] { "LocationId", "FacilityId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovementEvents",
                columns: table => new
                {
                    MovementEventId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnimalId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MovementReason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SourceType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Manual"),
                    RecordedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovementEvents", x => x.MovementEventId);
                    table.CheckConstraint("CK_MovementEvents_EndAfterStart", "[EndUtc] IS NULL OR [EndUtc] > [StartUtc]");
                    table.CheckConstraint("CK_MovementEvents_SourceType", "[SourceType] IN (N'Manual', N'Import', N'Derived')");
                    table.ForeignKey(
                        name: "FK_MovementEvents_Animals",
                        column: x => x.AnimalId,
                        principalTable: "Animals",
                        principalColumn: "AnimalId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovementEvents_Locations",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_Animals_AnimalNumber",
                table: "Animals",
                column: "AnimalNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Diseases_DiseaseCode",
                table: "Diseases",
                column: "DiseaseCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiseaseTraceProfiles_Disease_Active",
                table: "DiseaseTraceProfiles",
                columns: new[] { "DiseaseId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "UQ_DiseaseTraceProfiles_Disease",
                table: "DiseaseTraceProfiles",
                column: "DiseaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Facilities_FacilityCode",
                table: "Facilities",
                column: "FacilityCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_FacilityId",
                table: "ImportBatches",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportIssues_ImportBatchId",
                table: "ImportIssues",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationLinks_Facility_Type_Active",
                table: "LocationLinks",
                columns: new[] { "FacilityId", "LinkType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_LocationLinks_From_Type_Active",
                table: "LocationLinks",
                columns: new[] { "FromLocationId", "LinkType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_LocationLinks_FromLocationId_FacilityId",
                table: "LocationLinks",
                columns: new[] { "FromLocationId", "FacilityId" });

            migrationBuilder.CreateIndex(
                name: "IX_LocationLinks_To_Type_Active",
                table: "LocationLinks",
                columns: new[] { "ToLocationId", "LinkType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_LocationLinks_ToLocationId_FacilityId",
                table: "LocationLinks",
                columns: new[] { "ToLocationId", "FacilityId" });

            migrationBuilder.CreateIndex(
                name: "UX_LocationLinks_ActiveDirected",
                table: "LocationLinks",
                columns: new[] { "FromLocationId", "ToLocationId", "LinkType" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_Facility_Parent",
                table: "Locations",
                columns: new[] { "FacilityId", "ParentLocationId", "IsActive", "LocationType" });

            migrationBuilder.CreateIndex(
                name: "IX_Locations_Facility_Type_Active",
                table: "Locations",
                columns: new[] { "FacilityId", "LocationType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Locations_Parent_DisplayOrder",
                table: "Locations",
                columns: new[] { "ParentLocationId", "DisplayOrder", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Locations_ParentLocationId_FacilityId",
                table: "Locations",
                columns: new[] { "ParentLocationId", "FacilityId" });

            migrationBuilder.CreateIndex(
                name: "UQ_Locations_Facility_LocationCode",
                table: "Locations",
                columns: new[] { "FacilityId", "LocationCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Locations_ActiveKennelGridPosition",
                table: "Locations",
                columns: new[] { "ParentLocationId", "GridRow", "GridColumn", "StackLevel" },
                unique: true,
                filter: "[LocationType] = N'Kennel' AND [IsActive] = 1 AND [ParentLocationId] IS NOT NULL AND [GridRow] IS NOT NULL AND [GridColumn] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MovementEvents_Animal_Start_End",
                table: "MovementEvents",
                columns: new[] { "AnimalId", "StartUtc", "EndUtc", "LocationId" },
                descending: new[] { true, false, false, false });

            migrationBuilder.CreateIndex(
                name: "IX_MovementEvents_Location_Start_End",
                table: "MovementEvents",
                columns: new[] { "LocationId", "StartUtc", "EndUtc", "AnimalId" });

            migrationBuilder.CreateIndex(
                name: "IX_MovementEvents_OpenByLocation",
                table: "MovementEvents",
                columns: new[] { "LocationId", "AnimalId" },
                filter: "[EndUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_MovementEvents_OneOpenStayPerAnimal",
                table: "MovementEvents",
                column: "AnimalId",
                unique: true,
                filter: "[EndUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiseaseTraceProfileTopologyLinkTypes");

            migrationBuilder.DropTable(
                name: "ImportIssues");

            migrationBuilder.DropTable(
                name: "LocationLinks");

            migrationBuilder.DropTable(
                name: "MovementEvents");

            migrationBuilder.DropTable(
                name: "DiseaseTraceProfiles");

            migrationBuilder.DropTable(
                name: "ImportBatches");

            migrationBuilder.DropTable(
                name: "Animals");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropTable(
                name: "Diseases");

            migrationBuilder.DropTable(
                name: "Facilities");
        }
    }
}

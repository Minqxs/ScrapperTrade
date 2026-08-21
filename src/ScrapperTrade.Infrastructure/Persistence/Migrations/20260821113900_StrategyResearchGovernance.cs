using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrapperTrade.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StrategyResearchGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "strategy_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_strategy_definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "strategy_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StrategyDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    SpecificationJson = table.Column<string>(type: "TEXT", nullable: false),
                    SpecificationHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    LifecycleStatus = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_strategy_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_strategy_versions_strategy_definitions_StrategyDefinitionId",
                        column: x => x.StrategyDefinitionId,
                        principalTable: "strategy_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "backtest_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StrategyVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DatasetReference = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CostModelJson = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    IsOutOfSample = table.Column<bool>(type: "INTEGER", nullable: false),
                    PeriodStart = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backtest_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_backtest_runs_strategy_versions_StrategyVersionId",
                        column: x => x.StrategyVersionId,
                        principalTable: "strategy_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "research_candidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StrategyVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Hypothesis = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    AmbiguitiesJson = table.Column<string>(type: "TEXT", nullable: false),
                    HasUnresolvedAmbiguities = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_research_candidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_research_candidates_strategy_versions_StrategyVersionId",
                        column: x => x.StrategyVersionId,
                        principalTable: "strategy_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shadow_comparisons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChampionVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChallengerVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ChampionMetricsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ChallengerMetricsJson = table.Column<string>(type: "TEXT", nullable: false),
                    DecisionEvidenceJson = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shadow_comparisons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shadow_comparisons_strategy_versions_ChallengerVersionId",
                        column: x => x.ChallengerVersionId,
                        principalTable: "strategy_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shadow_comparisons_strategy_versions_ChampionVersionId",
                        column: x => x.ChampionVersionId,
                        principalTable: "strategy_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "strategy_activations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StrategyDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StrategyVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserConfirmationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApprovedByUser = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DeactivatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_strategy_activations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_strategy_activations_strategy_definitions_StrategyDefinitionId",
                        column: x => x.StrategyDefinitionId,
                        principalTable: "strategy_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_strategy_activations_strategy_versions_StrategyVersionId",
                        column: x => x.StrategyVersionId,
                        principalTable: "strategy_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "strategy_governance_audit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActorType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StrategyVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FromStatus = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ToStatus = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ConfirmationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_strategy_governance_audit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_strategy_governance_audit_strategy_versions_StrategyVersionId",
                        column: x => x.StrategyVersionId,
                        principalTable: "strategy_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "strategy_validation_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StrategyVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ValidationKind = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    EvidenceJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_strategy_validation_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_strategy_validation_runs_strategy_versions_StrategyVersionId",
                        column: x => x.StrategyVersionId,
                        principalTable: "strategy_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "backtest_metrics",
                columns: table => new
                {
                    BacktestRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TradeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpectancyR = table.Column<decimal>(type: "TEXT", nullable: false),
                    ProfitFactor = table.Column<decimal>(type: "TEXT", nullable: false),
                    MaximumDrawdownFraction = table.Column<decimal>(type: "TEXT", nullable: false),
                    NetReturnFraction = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backtest_metrics", x => x.BacktestRunId);
                    table.ForeignKey(
                        name: "FK_backtest_metrics_backtest_runs_BacktestRunId",
                        column: x => x.BacktestRunId,
                        principalTable: "backtest_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "backtest_trades",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BacktestRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Instrument = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Side = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    EnteredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExitedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EntryPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    ExitPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    RealisedR = table.Column<decimal>(type: "TEXT", nullable: false),
                    CostAmount = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backtest_trades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_backtest_trades_backtest_runs_BacktestRunId",
                        column: x => x.BacktestRunId,
                        principalTable: "backtest_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "research_candidate_provenance",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ResearchCandidateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SourceReference = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Citation = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Rationale = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_research_candidate_provenance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_research_candidate_provenance_research_candidates_ResearchCandidateId",
                        column: x => x.ResearchCandidateId,
                        principalTable: "research_candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "strategy_lineage",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ParentVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChildVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResearchCandidateId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Relationship = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_strategy_lineage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_strategy_lineage_research_candidates_ResearchCandidateId",
                        column: x => x.ResearchCandidateId,
                        principalTable: "research_candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_strategy_lineage_strategy_versions_ChildVersionId",
                        column: x => x.ChildVersionId,
                        principalTable: "strategy_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_strategy_lineage_strategy_versions_ParentVersionId",
                        column: x => x.ParentVersionId,
                        principalTable: "strategy_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_backtest_runs_StrategyVersionId",
                table: "backtest_runs",
                column: "StrategyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_backtest_trades_BacktestRunId",
                table: "backtest_trades",
                column: "BacktestRunId");

            migrationBuilder.CreateIndex(
                name: "IX_research_candidate_provenance_ResearchCandidateId",
                table: "research_candidate_provenance",
                column: "ResearchCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_research_candidates_StrategyVersionId",
                table: "research_candidates",
                column: "StrategyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_shadow_comparisons_ChallengerVersionId",
                table: "shadow_comparisons",
                column: "ChallengerVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_shadow_comparisons_ChampionVersionId_ChallengerVersionId",
                table: "shadow_comparisons",
                columns: new[] { "ChampionVersionId", "ChallengerVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_strategy_activations_StrategyDefinitionId",
                table: "strategy_activations",
                column: "StrategyDefinitionId",
                unique: true,
                filter: "DeactivatedAt IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_strategy_activations_StrategyVersionId",
                table: "strategy_activations",
                column: "StrategyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_strategy_activations_UserConfirmationId",
                table: "strategy_activations",
                column: "UserConfirmationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_strategy_definitions_Key",
                table: "strategy_definitions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_strategy_governance_audit_ConfirmationId",
                table: "strategy_governance_audit",
                column: "ConfirmationId",
                unique: true,
                filter: "ConfirmationId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_strategy_governance_audit_StrategyVersionId",
                table: "strategy_governance_audit",
                column: "StrategyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_strategy_lineage_ChildVersionId",
                table: "strategy_lineage",
                column: "ChildVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_strategy_lineage_ParentVersionId_ChildVersionId",
                table: "strategy_lineage",
                columns: new[] { "ParentVersionId", "ChildVersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_strategy_lineage_ResearchCandidateId",
                table: "strategy_lineage",
                column: "ResearchCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_strategy_validation_runs_StrategyVersionId_ValidationKind",
                table: "strategy_validation_runs",
                columns: new[] { "StrategyVersionId", "ValidationKind" });

            migrationBuilder.CreateIndex(
                name: "IX_strategy_versions_SpecificationHash",
                table: "strategy_versions",
                column: "SpecificationHash");

            migrationBuilder.CreateIndex(
                name: "IX_strategy_versions_StrategyDefinitionId_Version",
                table: "strategy_versions",
                columns: new[] { "StrategyDefinitionId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "backtest_metrics");

            migrationBuilder.DropTable(
                name: "backtest_trades");

            migrationBuilder.DropTable(
                name: "research_candidate_provenance");

            migrationBuilder.DropTable(
                name: "shadow_comparisons");

            migrationBuilder.DropTable(
                name: "strategy_activations");

            migrationBuilder.DropTable(
                name: "strategy_governance_audit");

            migrationBuilder.DropTable(
                name: "strategy_lineage");

            migrationBuilder.DropTable(
                name: "strategy_validation_runs");

            migrationBuilder.DropTable(
                name: "backtest_runs");

            migrationBuilder.DropTable(
                name: "research_candidates");

            migrationBuilder.DropTable(
                name: "strategy_versions");

            migrationBuilder.DropTable(
                name: "strategy_definitions");
        }
    }
}

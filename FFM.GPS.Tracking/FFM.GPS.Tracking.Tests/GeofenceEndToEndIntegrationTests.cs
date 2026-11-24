using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Application.Commands;
using FFM.GPS.Tracking.Application.DTOs;
using FFM.GPS.Tracking.Application.Handlers;
using FFM.GPS.Tracking.Application.Queries;
using FFM.GPS.Tracking.Domain.Entities;
using FFM.GPS.Tracking.Domain.Services;
using FFM.GPS.Tracking.Infrastructure;
using FFM.GPS.Tracking.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FFM.GPS.Tracking.Tests
{
    public class GeofenceEndToEndIntegrationTests
    {
        private GpsDbContext CreateInMemoryDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<GpsDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            return new GpsDbContext(options);
        }

        [Fact]
        public async Task EndToEnd_CreateCircularGeofence_AssignEmployee_ValidateInside_Success()
        {
            var db = CreateInMemoryDbContext("E2E_Circular_Success");
            var repo = new GeofenceRepository(db);
            var validationService = new GeofenceValidationService();

            var createHandler = new CreateGeofenceCommandHandler(repo);
            var assignHandler = new AssignGeofenceCommandHandler(repo);
            var validateHandler = new ValidateGeofenceCommandHandler(repo, validationService);

            // 1. Create Geofence
            var createCmd = new CreateGeofenceCommand
            {
                LocationRef = "GEOFENCE_SITE_102",
                Name = "Comptech Office",
                Type = "CIRCULAR",
                RadiusMeters = 100.0,
                Coordinates = new System.Collections.Generic.List<CoordinatePoint>
                {
                    new(13.0827, 80.2707)
                }
            };
            var created = await createHandler.Handle(createCmd, CancellationToken.None);
            Assert.NotNull(created);
            Assert.Equal("GEOFENCE_SITE_102", created.LocationRef);

            // 2. Assign to Employee 364
            var assignCmd = new AssignGeofenceCommand
            {
                EmployeeId = 364,
                GeofenceId = created.Id
            };
            var assigned = await assignHandler.Handle(assignCmd, CancellationToken.None);
            Assert.True(assigned);

            // 3. Validate Inside Coordinates (~20 meters from center)
            var validateCmd = new ValidateGeofenceCommand
            {
                EmployeeId = 364,
                Latitude = 13.0828,
                Longitude = 80.2708,
                GpsAccuracyMeters = 5.0
            };
            var result = await validateHandler.Handle(validateCmd, CancellationToken.None);

            Assert.True(result.IsValidGeofence);
            Assert.Equal("GEOFENCE_SITE_102", result.LocationRef);
            Assert.Equal("Comptech Office", result.SiteName);
            Assert.NotNull(result.DistanceFromCenterMeters);
        }

        [Fact]
        public async Task EndToEnd_ValidateOutside_ReturnsFalse()
        {
            var db = CreateInMemoryDbContext("E2E_Outside_Failure");
            var repo = new GeofenceRepository(db);
            var validationService = new GeofenceValidationService();

            var createHandler = new CreateGeofenceCommandHandler(repo);
            var assignHandler = new AssignGeofenceCommandHandler(repo);
            var validateHandler = new ValidateGeofenceCommandHandler(repo, validationService);

            // 1. Create Geofence
            var created = await createHandler.Handle(new CreateGeofenceCommand
            {
                LocationRef = "GEOFENCE_SITE_103",
                Name = "Branch North",
                Type = "CIRCULAR",
                RadiusMeters = 100.0,
                Coordinates = new System.Collections.Generic.List<CoordinatePoint>
                {
                    new(13.0827, 80.2707)
                }
            }, CancellationToken.None);

            // 2. Assign to Employee 365
            await assignHandler.Handle(new AssignGeofenceCommand
            {
                EmployeeId = 365,
                GeofenceId = created.Id
            }, CancellationToken.None);

            // 3. Validate Location 5 km away
            var result = await validateHandler.Handle(new ValidateGeofenceCommand
            {
                EmployeeId = 365,
                Latitude = 13.1500,
                Longitude = 80.3500
            }, CancellationToken.None);

            Assert.False(result.IsValidGeofence);
            Assert.Null(result.LocationRef);
        }

        [Fact]
        public async Task EndToEnd_PolygonGeofence_Validation_Success()
        {
            var db = CreateInMemoryDbContext("E2E_Polygon_Success");
            var repo = new GeofenceRepository(db);
            var validationService = new GeofenceValidationService();

            var createHandler = new CreateGeofenceCommandHandler(repo);
            var assignHandler = new AssignGeofenceCommandHandler(repo);
            var validateHandler = new ValidateGeofenceCommandHandler(repo, validationService);

            var created = await createHandler.Handle(new CreateGeofenceCommand
            {
                LocationRef = "GEOFENCE_POLYGON_SITE_1",
                Name = "Industrial Park",
                Type = "POLYGON",
                Coordinates = new System.Collections.Generic.List<CoordinatePoint>
                {
                    new(13.00, 80.00),
                    new(13.10, 80.00),
                    new(13.10, 80.10),
                    new(13.00, 80.10)
                }
            }, CancellationToken.None);

            await assignHandler.Handle(new AssignGeofenceCommand
            {
                EmployeeId = 366,
                GeofenceId = created.Id
            }, CancellationToken.None);

            var result = await validateHandler.Handle(new ValidateGeofenceCommand
            {
                EmployeeId = 366,
                Latitude = 13.05,
                Longitude = 80.05
            }, CancellationToken.None);

            Assert.True(result.IsValidGeofence);
            Assert.Equal("GEOFENCE_POLYGON_SITE_1", result.LocationRef);
        }

        [Fact]
        public async Task EndToEnd_DuplicateLocationRef_ThrowsException()
        {
            var db = CreateInMemoryDbContext("E2E_Duplicate_Ref");
            var repo = new GeofenceRepository(db);
            var createHandler = new CreateGeofenceCommandHandler(repo);

            await createHandler.Handle(new CreateGeofenceCommand
            {
                LocationRef = "GEOFENCE_UNIQUE_REF",
                Name = "Site 1",
                Type = "CIRCULAR",
                RadiusMeters = 50.0,
                Coordinates = new System.Collections.Generic.List<CoordinatePoint>
                {
                    new(13.0, 80.0)
                }
            }, CancellationToken.None);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                createHandler.Handle(new CreateGeofenceCommand
                {
                    LocationRef = "GEOFENCE_UNIQUE_REF",
                    Name = "Site 2 Duplicate",
                    Type = "CIRCULAR",
                    RadiusMeters = 50.0,
                    Coordinates = new System.Collections.Generic.List<CoordinatePoint>
                    {
                        new(13.0, 80.0)
                    }
                }, CancellationToken.None));
        }

        [Fact]
        public async Task EndToEnd_DuplicateAssignment_ThrowsException()
        {
            var db = CreateInMemoryDbContext("E2E_Duplicate_Assign");
            var repo = new GeofenceRepository(db);
            var createHandler = new CreateGeofenceCommandHandler(repo);
            var assignHandler = new AssignGeofenceCommandHandler(repo);

            var created = await createHandler.Handle(new CreateGeofenceCommand
            {
                LocationRef = "GEOFENCE_SITE_999",
                Name = "Site 999",
                Type = "CIRCULAR",
                RadiusMeters = 50.0,
                Coordinates = new System.Collections.Generic.List<CoordinatePoint>
                {
                    new(13.0, 80.0)
                }
            }, CancellationToken.None);

            await assignHandler.Handle(new AssignGeofenceCommand
            {
                EmployeeId = 367,
                GeofenceId = created.Id
            }, CancellationToken.None);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                assignHandler.Handle(new AssignGeofenceCommand
                {
                    EmployeeId = 367,
                    GeofenceId = created.Id
                }, CancellationToken.None));
        }
    }
}

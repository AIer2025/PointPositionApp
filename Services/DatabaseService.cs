using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using NLog;
using PointPositionApp.Models;

namespace PointPositionApp.Services
{
    /// <summary>数据库服务 - 基于 SQLite + Dapper</summary>
    public class DatabaseService : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly string _connectionString;
        private SqliteConnection? _connection;
        private readonly SemaphoreSlim _dbLock = new(1, 1);
        private bool _disposed;

        public bool IsConnected => _connection?.State == ConnectionState.Open;

        public DatabaseService(string dbPath)
        {
            _connectionString = $"Data Source={dbPath}";
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                _connection = new SqliteConnection(_connectionString);
                await _connection.OpenAsync();
                await InitializeTablesAsync();
                Logger.Info("数据库连接成功: {0}", _connectionString);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "数据库连接失败");
                return false;
            }
        }

        private async Task InitializeTablesAsync()
        {
            if (_connection == null) return;

            var sql = @"
CREATE TABLE IF NOT EXISTS Project (
    ProjectId INTEGER PRIMARY KEY AUTOINCREMENT,
    ProjectName VARCHAR(100) NOT NULL,
    Description TEXT,
    CreateTime DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS ClawInfo (
    ClawInfoId INTEGER PRIMARY KEY AUTOINCREMENT,
    Description VARCHAR(200),
    OpenPos REAL DEFAULT 0,
    ClosePos REAL DEFAULT 0,
    Angle REAL DEFAULT 0,
    CloseTorque REAL DEFAULT 0,
    OpenTorque REAL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS Module (
    ModuleId INTEGER PRIMARY KEY AUTOINCREMENT,
    ProjectId INTEGER NOT NULL,
    ChannelGroupName VARCHAR(100) NOT NULL,
    RowFirst BOOLEAN DEFAULT 1,
    Rows INTEGER DEFAULT 1,
    Cols INTEGER DEFAULT 1,
    SpaceRow REAL DEFAULT 0,
    SpaceCol REAL DEFAULT 0,
    StartPosition_X REAL DEFAULT 0,
    StartPosition_Y REAL DEFAULT 0,
    StartPosition_Z REAL DEFAULT 0,
    StartPosition_Z1 REAL DEFAULT 0,
    StartPosition_R REAL DEFAULT 0,
    ClawInfoId INTEGER,
    FOREIGN KEY (ProjectId) REFERENCES Project(ProjectId),
    FOREIGN KEY (ClawInfoId) REFERENCES ClawInfo(ClawInfoId)
);

CREATE TABLE IF NOT EXISTS Tray (
    TrayId INTEGER PRIMARY KEY AUTOINCREMENT,
    ProjectId INTEGER NOT NULL,
    LabTrayCode VARCHAR(50),
    LabTrayCategory VARCHAR(50),
    LabTrayName VARCHAR(100),
    LabTrayDescription TEXT,
    Rows INTEGER DEFAULT 1,
    Cols INTEGER DEFAULT 1,
    SpaceRow REAL DEFAULT 0,
    SpaceCol REAL DEFAULT 0,
    WellDiameter REAL DEFAULT 0,
    LiquidStep REAL DEFAULT 0,
    RowLabels TEXT,
    ColLabels TEXT,
    FOREIGN KEY (ProjectId) REFERENCES Project(ProjectId)
);

CREATE TABLE IF NOT EXISTS TrayCoordinateRegion (
    RegionId INTEGER PRIMARY KEY AUTOINCREMENT,
    TrayId INTEGER NOT NULL,
    RegionName VARCHAR(100),
    StartPosition_X REAL DEFAULT 0,
    StartPosition_Y REAL DEFAULT 0,
    StartPosition_Z REAL DEFAULT 0,
    StartPosition_Z1 REAL DEFAULT 0,
    StartPosition_R REAL DEFAULT 0,
    Rows INTEGER DEFAULT 1,
    Cols INTEGER DEFAULT 1,
    SpaceRow REAL DEFAULT 0,
    SpaceCol REAL DEFAULT 0,
    RowFirst BOOLEAN DEFAULT 1,
    HasCover BOOLEAN DEFAULT 0,
    CanAspirateCount INTEGER DEFAULT 0,
    CanDispenseCount INTEGER DEFAULT 0,
    RowLabels TEXT,
    ColLabels TEXT,
    ClawInfoId INTEGER,
    FOREIGN KEY (TrayId) REFERENCES Tray(TrayId),
    FOREIGN KEY (ClawInfoId) REFERENCES ClawInfo(ClawInfoId)
);

CREATE TABLE IF NOT EXISTS PointPosition (
    PointId INTEGER PRIMARY KEY AUTOINCREMENT,
    OwnerType VARCHAR(20) NOT NULL,
    OwnerId INTEGER NOT NULL,
    RowIndex INTEGER NOT NULL,
    ColIndex INTEGER NOT NULL,
    X REAL DEFAULT 0,
    Y REAL DEFAULT 0,
    Z REAL DEFAULT 0,
    Z1 REAL DEFAULT 0,
    R REAL DEFAULT 0,
    ClawInfoId INTEGER,
    FOREIGN KEY (ClawInfoId) REFERENCES ClawInfo(ClawInfoId)
);

CREATE INDEX IF NOT EXISTS idx_point_owner ON PointPosition(OwnerType, OwnerId);
CREATE INDEX IF NOT EXISTS idx_module_project ON Module(ProjectId);
CREATE INDEX IF NOT EXISTS idx_tray_project ON Tray(ProjectId);
CREATE INDEX IF NOT EXISTS idx_region_tray ON TrayCoordinateRegion(TrayId);
";
            await _connection.ExecuteAsync(sql);
            Logger.Info("数据库表初始化完成");
        }

        #region Project
        public async Task<List<Project>> GetAllProjectsAsync()
        {
            if (_connection == null) return new();
            await _dbLock.WaitAsync();
            try
            {
                return (await _connection.QueryAsync<Project>("SELECT * FROM Project ORDER BY CreateTime DESC")).ToList();
            }
            finally { _dbLock.Release(); }
        }
        #endregion

        #region Module
        public async Task<List<Module>> GetModulesByProjectAsync(int projectId)
        {
            if (_connection == null) return new();
            await _dbLock.WaitAsync();
            try
            {
                return (await _connection.QueryAsync<Module>(
                    "SELECT * FROM Module WHERE ProjectId = @ProjectId", new { ProjectId = projectId })).ToList();
            }
            finally { _dbLock.Release(); }
        }

        public async Task<Module?> GetModuleAsync(int moduleId)
        {
            if (_connection == null) return null;
            await _dbLock.WaitAsync();
            try
            {
                return await _connection.QueryFirstOrDefaultAsync<Module>(
                    "SELECT * FROM Module WHERE ModuleId = @ModuleId", new { ModuleId = moduleId });
            }
            finally { _dbLock.Release(); }
        }
        #endregion

        #region Tray
        public async Task<List<Tray>> GetTraysByProjectAsync(int projectId)
        {
            if (_connection == null) return new();
            await _dbLock.WaitAsync();
            try
            {
                return (await _connection.QueryAsync<Tray>(
                    "SELECT * FROM Tray WHERE ProjectId = @ProjectId", new { ProjectId = projectId })).ToList();
            }
            finally { _dbLock.Release(); }
        }

        public async Task<Tray?> GetTrayAsync(int trayId)
        {
            if (_connection == null) return null;
            await _dbLock.WaitAsync();
            try
            {
                return await _connection.QueryFirstOrDefaultAsync<Tray>(
                    "SELECT * FROM Tray WHERE TrayId = @TrayId", new { TrayId = trayId });
            }
            finally { _dbLock.Release(); }
        }
        #endregion

        #region TrayCoordinateRegion
        public async Task<List<TrayCoordinateRegion>> GetRegionsByTrayAsync(int trayId)
        {
            if (_connection == null) return new();
            await _dbLock.WaitAsync();
            try
            {
                return (await _connection.QueryAsync<TrayCoordinateRegion>(
                    "SELECT * FROM TrayCoordinateRegion WHERE TrayId = @TrayId", new { TrayId = trayId })).ToList();
            }
            finally { _dbLock.Release(); }
        }
        #endregion

        #region ClawInfo
        public async Task<ClawInfo?> GetClawInfoAsync(int clawInfoId)
        {
            if (_connection == null) return null;
            await _dbLock.WaitAsync();
            try
            {
                return await _connection.QueryFirstOrDefaultAsync<ClawInfo>(
                    "SELECT * FROM ClawInfo WHERE ClawInfoId = @Id", new { Id = clawInfoId });
            }
            finally { _dbLock.Release(); }
        }

        public async Task<int> SaveClawInfoAsync(ClawInfo info)
        {
            if (_connection == null) return 0;
            await _dbLock.WaitAsync();
            try
            {
                if (info.ClawInfoId > 0)
                {
                    await _connection.ExecuteAsync(@"
UPDATE ClawInfo SET Description=@Description, OpenPos=@OpenPos, ClosePos=@ClosePos,
    Angle=@Angle, CloseTorque=@CloseTorque, OpenTorque=@OpenTorque
WHERE ClawInfoId=@ClawInfoId", info);
                    return info.ClawInfoId;
                }
                else
                {
                    return await _connection.ExecuteScalarAsync<int>(@"
INSERT INTO ClawInfo (Description, OpenPos, ClosePos, Angle, CloseTorque, OpenTorque)
VALUES (@Description, @OpenPos, @ClosePos, @Angle, @CloseTorque, @OpenTorque);
SELECT last_insert_rowid();", info);
                }
            }
            finally { _dbLock.Release(); }
        }
        #endregion

        #region PointPosition
        public async Task<List<PointPosition>> GetPointsAsync(string ownerType, int ownerId)
        {
            if (_connection == null) return new();
            await _dbLock.WaitAsync();
            try
            {
                return (await _connection.QueryAsync<PointPosition>(
                    "SELECT * FROM PointPosition WHERE OwnerType=@OwnerType AND OwnerId=@OwnerId ORDER BY RowIndex, ColIndex",
                    new { OwnerType = ownerType, OwnerId = ownerId })).ToList();
            }
            finally { _dbLock.Release(); }
        }

        public async Task<PointPosition?> GetPointAsync(string ownerType, int ownerId, int rowIndex, int colIndex)
        {
            if (_connection == null) return null;
            await _dbLock.WaitAsync();
            try
            {
                return await _connection.QueryFirstOrDefaultAsync<PointPosition>(
                    "SELECT * FROM PointPosition WHERE OwnerType=@OwnerType AND OwnerId=@OwnerId AND RowIndex=@RowIndex AND ColIndex=@ColIndex",
                    new { OwnerType = ownerType, OwnerId = ownerId, RowIndex = rowIndex, ColIndex = colIndex });
            }
            finally { _dbLock.Release(); }
        }

        public async Task SavePointAsync(PointPosition point)
        {
            if (_connection == null) return;
            await _dbLock.WaitAsync();
            try
            {
                var existing = await _connection.QueryFirstOrDefaultAsync<PointPosition>(
                    "SELECT * FROM PointPosition WHERE OwnerType=@OwnerType AND OwnerId=@OwnerId AND RowIndex=@RowIndex AND ColIndex=@ColIndex",
                    new { point.OwnerType, point.OwnerId, point.RowIndex, point.ColIndex });
                if (existing != null)
                {
                    point.PointId = existing.PointId;
                    await _connection.ExecuteAsync(@"
UPDATE PointPosition SET X=@X, Y=@Y, Z=@Z, Z1=@Z1, R=@R, ClawInfoId=@ClawInfoId
WHERE PointId=@PointId", point);
                }
                else
                {
                    await _connection.ExecuteAsync(@"
INSERT INTO PointPosition (OwnerType, OwnerId, RowIndex, ColIndex, X, Y, Z, Z1, R, ClawInfoId)
VALUES (@OwnerType, @OwnerId, @RowIndex, @ColIndex, @X, @Y, @Z, @Z1, @R, @ClawInfoId)", point);
                }
            }
            finally { _dbLock.Release(); }
            Logger.Debug("保存点位: [{0},{1}] -> ({2:F3},{3:F3},{4:F3},{5:F3},{6:F3})",
                point.RowIndex, point.ColIndex, point.X, point.Y, point.Z, point.Z1, point.R);
        }

        public async Task DeletePointAsync(string ownerType, int ownerId, int rowIndex, int colIndex)
        {
            if (_connection == null) return;
            await _dbLock.WaitAsync();
            try
            {
                await _connection.ExecuteAsync(
                    "DELETE FROM PointPosition WHERE OwnerType=@OwnerType AND OwnerId=@OwnerId AND RowIndex=@RowIndex AND ColIndex=@ColIndex",
                    new { OwnerType = ownerType, OwnerId = ownerId, RowIndex = rowIndex, ColIndex = colIndex });
            }
            finally { _dbLock.Release(); }
        }

        public async Task CopyPointsToRowAsync(string ownerType, int ownerId, int sourceRow, int targetRow, int cols, double rowSpacingY)
        {
            if (_connection == null) return;
            await _dbLock.WaitAsync();
            try
            {
                var sourcePoints = (await _connection.QueryAsync<PointPosition>(
                    "SELECT * FROM PointPosition WHERE OwnerType=@OwnerType AND OwnerId=@OwnerId AND RowIndex=@RowIndex",
                    new { OwnerType = ownerType, OwnerId = ownerId, RowIndex = sourceRow })).ToList();

                double deltaY = (targetRow - sourceRow) * rowSpacingY;
                foreach (var sp in sourcePoints)
                {
                    var existing = await _connection.QueryFirstOrDefaultAsync<PointPosition>(
                        "SELECT * FROM PointPosition WHERE OwnerType=@OwnerType AND OwnerId=@OwnerId AND RowIndex=@RowIndex AND ColIndex=@ColIndex",
                        new { OwnerType = ownerType, OwnerId = ownerId, RowIndex = targetRow, ColIndex = sp.ColIndex });

                    var newPoint = new PointPosition
                    {
                        OwnerType = ownerType,
                        OwnerId = ownerId,
                        RowIndex = targetRow,
                        ColIndex = sp.ColIndex,
                        X = sp.X,
                        Y = sp.Y + deltaY,
                        Z = sp.Z,
                        Z1 = sp.Z1,
                        R = sp.R,
                        ClawInfoId = sp.ClawInfoId
                    };

                    if (existing != null)
                    {
                        newPoint.PointId = existing.PointId;
                        await _connection.ExecuteAsync(@"
UPDATE PointPosition SET X=@X, Y=@Y, Z=@Z, Z1=@Z1, R=@R, ClawInfoId=@ClawInfoId
WHERE PointId=@PointId", newPoint);
                    }
                    else
                    {
                        await _connection.ExecuteAsync(@"
INSERT INTO PointPosition (OwnerType, OwnerId, RowIndex, ColIndex, X, Y, Z, Z1, R, ClawInfoId)
VALUES (@OwnerType, @OwnerId, @RowIndex, @ColIndex, @X, @Y, @Z, @Z1, @R, @ClawInfoId)", newPoint);
                    }
                }
                Logger.Info("批量复制点位: 行{0} -> 行{1}, 偏移Y={2:F3}", sourceRow, targetRow, deltaY);
            }
            finally { _dbLock.Release(); }
        }
        #endregion

        #region Demo Data
        public async Task InsertDemoDataAsync()
        {
            if (_connection == null) return;
            var count = await _connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Project");
            if (count > 0) return;

            // 插入演示夹爪
            await _connection.ExecuteAsync(@"
INSERT INTO ClawInfo (Description, OpenPos, ClosePos, Angle, CloseTorque, OpenTorque)
VALUES ('默认夹爪', 50.0, 5.0, 0, 20.0, 15.0)");

            // 插入演示项目
            await _connection.ExecuteAsync(@"
INSERT INTO Project (ProjectName, Description) VALUES ('演示项目', '用于功能测试的演示项目')");

            // 插入演示模块
            await _connection.ExecuteAsync(@"
INSERT INTO Module (ProjectId, ChannelGroupName, RowFirst, Rows, Cols, SpaceRow, SpaceCol,
    StartPosition_X, StartPosition_Y, StartPosition_Z, StartPosition_Z1, StartPosition_R, ClawInfoId)
VALUES (1, '试管架模块', 1, 4, 6, 18.0, 18.0, 100.0, 50.0, 30.0, 25.0, 0.0, 1)");

            // 插入演示托盘
            await _connection.ExecuteAsync(@"
INSERT INTO Tray (ProjectId, LabTrayCode, LabTrayCategory, LabTrayName, LabTrayDescription,
    Rows, Cols, SpaceRow, SpaceCol, WellDiameter, LiquidStep, RowLabels, ColLabels)
VALUES (1, 'TRAY-96', '96孔板', '标准96孔板', '8行12列标准微孔板',
    8, 12, 9.0, 9.0, 6.5, 0.5,
    '[""A"",""B"",""C"",""D"",""E"",""F"",""G"",""H""]',
    '[""1"",""2"",""3"",""4"",""5"",""6"",""7"",""8"",""9"",""10"",""11"",""12""]')");

            // 插入演示区域
            await _connection.ExecuteAsync(@"
INSERT INTO TrayCoordinateRegion (TrayId, RegionName, StartPosition_X, StartPosition_Y,
    StartPosition_Z, StartPosition_Z1, StartPosition_R, Rows, Cols, SpaceRow, SpaceCol,
    RowFirst, HasCover, CanAspirateCount, CanDispenseCount, ClawInfoId)
VALUES (1, '默认区域', 200.0, 100.0, 40.0, 35.0, 0.0, 8, 12, 9.0, 9.0, 1, 0, 1, 1, 1)");

            Logger.Info("演示数据插入完成");
        }
        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _connection?.Close();
            _connection?.Dispose();
            _dbLock.Dispose();
            Logger.Info("数据库连接已关闭");
        }
    }
}

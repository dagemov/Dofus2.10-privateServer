SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Username NVARCHAR(50) = N'sebcos1';
DECLARE @ServerId SMALLINT = 1;
DECLARE @CharacterName NVARCHAR(30) = N'AlohaTest';
DECLARE @BreedId TINYINT = 1; -- 1=Feca, 2=Osamodas, 3=Enutrof, 4=Sram, 5=Xelor, 6=Ecaflip, 7=Eniripsa, 8=Iop, 9=Cra, 10=Sadida, 11=Sacrieur, 12=Pandawa, 13=Roublard, 14=Zobal, 15=Steamer
DECLARE @Sex BIT = 0; -- 0=male, 1=female
DECLARE @CosmeticId SMALLINT = 0;
DECLARE @Color1 INT = 0;
DECLARE @Color2 INT = 0;
DECLARE @Color3 INT = 0;
DECLARE @Color4 INT = 0;
DECLARE @Color5 INT = 0;

DECLARE @SpawnMapId INT = 80217091;
DECLARE @SpawnCellId SMALLINT = 300;
DECLARE @SpawnDirection TINYINT = 2;

DECLARE @AccountId INT;
DECLARE @CharacterId BIGINT;
DECLARE @BonesId INT;
DECLARE @SkinId INT;

SELECT @AccountId = Id
FROM dbo.Accounts
WHERE Username = @Username;

IF @AccountId IS NULL
BEGIN
    THROW 50001, 'The account username does not exist.', 1;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.GameServers WHERE Id = @ServerId)
BEGIN
    THROW 50002, 'The target game server does not exist.', 1;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Breeds WHERE Id = @BreedId AND IsPlayable = 1)
BEGIN
    THROW 50003, 'The selected breed does not exist or is not playable.', 1;
END;

IF EXISTS (SELECT 1 FROM dbo.Characters WHERE GameServerId = @ServerId AND Name = @CharacterName)
BEGIN
    THROW 50004, 'The character name already exists on that server.', 1;
END;

SELECT
    @BonesId =
        CASE @BreedId
            WHEN 1 THEN 1
            WHEN 2 THEN 1
            WHEN 3 THEN 1
            WHEN 4 THEN 1
            WHEN 5 THEN 1
            WHEN 6 THEN 1
            WHEN 7 THEN 1
            WHEN 8 THEN 1
            WHEN 9 THEN 1
            WHEN 10 THEN 1
            WHEN 11 THEN 1
            WHEN 12 THEN 1
            WHEN 13 THEN 1
            WHEN 14 THEN 1
            WHEN 15 THEN 1
        END,
    @SkinId =
        CASE
            WHEN @Sex = 0 THEN
                CASE @BreedId
                    WHEN 1 THEN 10
                    WHEN 2 THEN 20
                    WHEN 3 THEN 30
                    WHEN 4 THEN 40
                    WHEN 5 THEN 50
                    WHEN 6 THEN 60
                    WHEN 7 THEN 70
                    WHEN 8 THEN 80
                    WHEN 9 THEN 90
                    WHEN 10 THEN 100
                    WHEN 11 THEN 110
                    WHEN 12 THEN 120
                    WHEN 13 THEN 1405
                    WHEN 14 THEN 1437
                    WHEN 15 THEN 1663
                END
            ELSE
                CASE @BreedId
                    WHEN 1 THEN 11
                    WHEN 2 THEN 21
                    WHEN 3 THEN 31
                    WHEN 4 THEN 41
                    WHEN 5 THEN 51
                    WHEN 6 THEN 61
                    WHEN 7 THEN 71
                    WHEN 8 THEN 81
                    WHEN 9 THEN 91
                    WHEN 10 THEN 101
                    WHEN 11 THEN 111
                    WHEN 12 THEN 121
                    WHEN 13 THEN 1407
                    WHEN 14 THEN 1438
                    WHEN 15 THEN 1664
                END
        END;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO dbo.Characters
    (
        AccountId,
        GameServerId,
        BreedId,
        Name,
        Sex,
        Level,
        Experience,
        CosmeticId,
        Color1,
        Color2,
        Color3,
        Color4,
        Color5,
        BonesId,
        SkinId,
        CreatedAtUtc
    )
    VALUES
    (
        @AccountId,
        @ServerId,
        @BreedId,
        @CharacterName,
        @Sex,
        1,
        0,
        @CosmeticId,
        @Color1,
        @Color2,
        @Color3,
        @Color4,
        @Color5,
        @BonesId,
        @SkinId,
        SYSUTCDATETIME()
    );

    SET @CharacterId = CAST(SCOPE_IDENTITY() AS BIGINT);

    INSERT INTO dbo.CharacterStats
    (
        CharacterId,
        Kamas,
        StatsPoints,
        SpellsPoints,
        LifePoints,
        MaxLifePoints,
        EnergyPoints,
        MaxEnergyPoints,
        ActionPoints,
        MovementPoints
    )
    VALUES
    (
        @CharacterId,
        0,
        0,
        0,
        50,
        50,
        10000,
        10000,
        6,
        3
    );

    INSERT INTO dbo.CharacterPositions
    (
        CharacterId,
        MapId,
        CellId,
        Direction
    )
    VALUES
    (
        @CharacterId,
        @SpawnMapId,
        @SpawnCellId,
        @SpawnDirection
    );

    COMMIT TRANSACTION;

    SELECT
        @CharacterId AS CharacterId,
        @AccountId AS AccountId,
        @Username AS Username,
        @ServerId AS GameServerId,
        @CharacterName AS CharacterName,
        @BreedId AS BreedId,
        @Sex AS Sex,
        @SpawnMapId AS MapId,
        @SpawnCellId AS CellId;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    THROW;
END CATCH;

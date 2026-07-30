-- Consolidation des sections : Maternelle, Primaire, Secondaire générale, Humanité
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

USE [SchoolManagementRDC];
GO

DECLARE @SchoolId UNIQUEIDENTIFIER;
SELECT TOP 1 @SchoolId = Id FROM Schools WHERE IsDeleted = 0 ORDER BY CreatedAt;

IF @SchoolId IS NULL
BEGIN
    PRINT N'Aucune école active trouvée.';
    RETURN;
END

PRINT N'Consolidation des sections pour l''école ' + CAST(@SchoolId AS nvarchar(36));

-- 1. Upsert des 4 sections canoniques
MERGE Sections AS target
USING (VALUES
    (@SchoolId, N'MAT', N'Maternelle', 1),
    (@SchoolId, N'PRI', N'Primaire', 1),
    (@SchoolId, N'CTEB', N'Secondaire générale', 2),
    (@SchoolId, N'HUM', N'Humanité', 2)
) AS source (SchoolId, Code, Name, Cycle)
ON target.SchoolId = source.SchoolId AND target.Code = source.Code AND target.IsDeleted = 0
WHEN MATCHED THEN
    UPDATE SET Name = source.Name, Cycle = source.Cycle
WHEN NOT MATCHED THEN
    INSERT (Id, SchoolId, Code, Name, Cycle, CreatedAt, IsDeleted)
    VALUES (NEWID(), source.SchoolId, source.Code, source.Name, source.Cycle, SYSUTCDATETIME(), 0);

-- 2. Réaffecter les salles selon la classe pédagogique
UPDATE cr
SET cr.SectionId = targetSection.Id
FROM ClassRooms cr
JOIN PedagogicalClasses pc ON pc.Id = cr.PedagogicalClassId AND pc.IsDeleted = 0
JOIN Sections targetSection ON targetSection.SchoolId = cr.SchoolId AND targetSection.IsDeleted = 0
    AND targetSection.Code = CASE pc.Program
        WHEN 1 THEN N'MAT'   -- Maternelle
        WHEN 2 THEN N'PRI'   -- Primaire
        WHEN 3 THEN N'CTEB'  -- Secondaire générale (7e-8e)
        ELSE N'HUM'          -- Humanité (Humanités, HPRO, FS)
    END
WHERE cr.IsDeleted = 0
  AND cr.PedagogicalClassId IS NOT NULL
  AND cr.SectionId <> targetSection.Id;

PRINT N'Salles réaffectées (via classe pédagogique) : ' + CAST(@@ROWCOUNT AS nvarchar(10));

-- 3. Réaffecter les salles restantes selon l'ancien code section
UPDATE cr
SET cr.SectionId = targetSection.Id
FROM ClassRooms cr
JOIN Sections currentSection ON currentSection.Id = cr.SectionId AND currentSection.IsDeleted = 0
JOIN Sections targetSection ON targetSection.SchoolId = cr.SchoolId AND targetSection.IsDeleted = 0
    AND targetSection.Code = CASE currentSection.Code
        WHEN N'MAT' THEN N'MAT'
        WHEN N'PRI' THEN N'PRI'
        WHEN N'PRIM' THEN N'PRI'
        WHEN N'CTEB' THEN N'CTEB'
        WHEN N'HUM' THEN N'HUM'
        WHEN N'SEC-SCI' THEN N'HUM'
        WHEN N'SEC-LIT' THEN N'HUM'
        WHEN N'HPRO' THEN N'HUM'
        WHEN N'FS' THEN N'HUM'
        ELSE N'HUM'
    END
WHERE cr.IsDeleted = 0
  AND cr.SectionId <> targetSection.Id;

PRINT N'Salles réaffectées (via code section legacy) : ' + CAST(@@ROWCOUNT AS nvarchar(10));

-- 4. Supprimer (soft-delete) les sections non canoniques
UPDATE s
SET s.IsDeleted = 1
FROM Sections s
WHERE s.SchoolId = @SchoolId
  AND s.IsDeleted = 0
  AND s.Code NOT IN (N'MAT', N'PRI', N'CTEB', N'HUM')
  AND NOT EXISTS (
      SELECT 1 FROM ClassRooms cr
      WHERE cr.SectionId = s.Id AND cr.IsDeleted = 0
  );

PRINT N'Sections obsolètes supprimées : ' + CAST(@@ROWCOUNT AS nvarchar(10));

-- 5. Purge physique des sections obsolètes déjà soft-deleted (aucune salle liée)
DELETE s
FROM Sections s
WHERE s.SchoolId = @SchoolId
  AND s.IsDeleted = 1
  AND s.Code NOT IN (N'MAT', N'PRI', N'CTEB', N'HUM')
  AND NOT EXISTS (
      SELECT 1 FROM ClassRooms cr WHERE cr.SectionId = s.Id
  );

PRINT N'Sections obsolètes purgées définitivement : ' + CAST(@@ROWCOUNT AS nvarchar(10));

SELECT Code, Name, Cycle, IsDeleted
FROM Sections
WHERE SchoolId = @SchoolId AND IsDeleted = 0
ORDER BY Cycle, Code;
GO

-- Diagnostic en lecture seule, à exécuter dans la base locale actuelle du client.
-- Aucun UPDATE, DELETE, INSERT ni changement de configuration.
SELECT Id, Name, IsActive, IsDeleted FROM dbo.Schools ORDER BY Name;

SELECT s.SchoolId AS MissingSchoolId, COUNT_BIG(*) AS StudentCount
FROM dbo.Students s
LEFT JOIN dbo.Schools school ON school.Id=s.SchoolId
WHERE school.Id IS NULL
GROUP BY s.SchoolId;

SELECT COUNT_BIG(*) AS TotalStudents,
    SUM(CASE WHEN s.IsDeleted=0 THEN CONVERT(bigint,1) ELSE 0 END) AS ActiveStudents,
    SUM(CASE WHEN school.Id IS NULL THEN CONVERT(bigint,1) ELSE 0 END) AS StudentsWithMissingSchool
FROM dbo.Students s
LEFT JOIN dbo.Schools school ON school.Id=s.SchoolId;

/*
  Lot 2B-5B — GRANTs SQL labo UNIQUEMENT.
  Cible : SchoolManagementRDC_UpdateIntegration
  NE PAS exécuter contre SchoolManagementRDC, _Development, _Production.

  Architecture C : UpdateAgent n'est PAS owner.
  Restore = master.dbo.ErpScolaire_RestoreSchoolDatabase (procédure signée).

  Appliquer via scripts/update-agent-restore-signed-proc-lab.sql
  (certificat, login certificat owner, ua_migrator, EXECUTE).
*/

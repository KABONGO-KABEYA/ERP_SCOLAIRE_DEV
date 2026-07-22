import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';
import 'package:intl/intl.dart';

import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';
import '../enrollment/models/enrollment_models.dart';
import 'models/secretary_student_models.dart';

class SecretaryStudentDossierScreen extends ConsumerStatefulWidget {
  const SecretaryStudentDossierScreen({super.key, required this.studentId});

  final String studentId;

  @override
  ConsumerState<SecretaryStudentDossierScreen> createState() => _SecretaryStudentDossierScreenState();
}

class _SecretaryStudentDossierScreenState extends ConsumerState<SecretaryStudentDossierScreen> {
  final _imagePicker = ImagePicker();
  StudentProfile? _profile;
  List<StudentDocument> _documents = [];
  bool _loading = true;
  bool _busy = false;
  String? _error;

  bool get _canEdit => ref.read(writePolicyProvider).canMutateBusinessData;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final repo = ref.read(secretaryStudentRepositoryProvider);
      final profile = await repo.getProfile(widget.studentId);
      final docs = await repo.listDocuments(widget.studentId);
      if (!mounted) return;
      setState(() {
        _profile = profile;
        _documents = docs;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _refreshDocuments() async {
    final docs = await ref.read(secretaryStudentRepositoryProvider).listDocuments(widget.studentId);
    if (!mounted) return;
    setState(() => _documents = docs);
  }

  String _formatDate(String raw) {
    final dt = DateTime.tryParse(raw);
    if (dt == null) return raw;
    return DateFormat('dd/MM/yyyy').format(dt.toLocal());
  }

  String _formatBytes(int bytes) {
    if (bytes < 1024) return '$bytes o';
    if (bytes < 1024 * 1024) return '${(bytes / 1024).toStringAsFixed(0)} Ko';
    return '${(bytes / (1024 * 1024)).toStringAsFixed(1)} Mo';
  }

  Future<void> _ensureCanEdit() async {
    if (_canEdit) return;
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(ref.read(writePolicyProvider).readOnlyHint)),
    );
    throw StateError('read-only');
  }

  Future<void> _addOrReplaceDocument(String documentType) async {
    try {
      await _ensureCanEdit();
    } catch (_) {
      return;
    }

    if (documentType == 'Photo') {
      await _showPhotoSourceSheet(documentType);
      return;
    }

    final result = await FilePicker.platform.pickFiles(withData: true);
    if (result == null || result.files.isEmpty) return;
    final file = result.files.first;
    if (file.path == null && (file.bytes == null || file.bytes!.isEmpty)) {
      _toast('Impossible de lire le fichier sélectionné.');
      return;
    }
    await _upload(
      documentType: documentType,
      fileName: file.name,
      filePath: file.path,
      fileBytes: file.bytes,
      replaceExistingOfSameType: false,
    );
  }

  Future<void> _showPhotoSourceSheet(String documentType) async {
    await showModalBottomSheet<void>(
      context: context,
      builder: (context) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Padding(
              padding: EdgeInsets.fromLTRB(16, 16, 16, 8),
              child: Text(
                'Photo d\'identité',
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 16),
              ),
            ),
            ListTile(
              leading: const Icon(Icons.photo_camera),
              title: const Text('Prendre une photo'),
              onTap: () {
                Navigator.pop(context);
                _capturePhoto(documentType);
              },
            ),
            ListTile(
              leading: const Icon(Icons.photo_library),
              title: const Text('Choisir depuis la galerie'),
              onTap: () {
                Navigator.pop(context);
                _pickPhotoFromGallery(documentType);
              },
            ),
            ListTile(
              leading: const Icon(Icons.upload_file),
              title: const Text('Importer un fichier'),
              onTap: () async {
                Navigator.pop(context);
                final result = await FilePicker.platform.pickFiles(
                  type: FileType.image,
                  withData: true,
                );
                if (result == null || result.files.isEmpty) return;
                final file = result.files.first;
                if (file.path == null && (file.bytes == null || file.bytes!.isEmpty)) {
                  _toast('Impossible de lire l\'image sélectionnée.');
                  return;
                }
                await _upload(
                  documentType: documentType,
                  fileName: _photoFileName(file.name),
                  filePath: file.path,
                  fileBytes: file.bytes,
                  replaceExistingOfSameType: true,
                );
              },
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _capturePhoto(String documentType) async {
    final photo = await _imagePicker.pickImage(
      source: ImageSource.camera,
      preferredCameraDevice: CameraDevice.front,
      maxWidth: 1200,
      imageQuality: 85,
    );
    if (photo == null) return;
    await _upload(
      documentType: documentType,
      fileName: _photoFileName(photo.name),
      filePath: photo.path,
      replaceExistingOfSameType: true,
    );
  }

  Future<void> _pickPhotoFromGallery(String documentType) async {
    final photo = await _imagePicker.pickImage(
      source: ImageSource.gallery,
      maxWidth: 1200,
      imageQuality: 85,
    );
    if (photo == null) return;
    await _upload(
      documentType: documentType,
      fileName: _photoFileName(photo.name),
      filePath: photo.path,
      replaceExistingOfSameType: true,
    );
  }

  String _photoFileName(String originalName) {
    final lower = originalName.toLowerCase();
    if (lower.endsWith('.jpg') || lower.endsWith('.jpeg') || lower.endsWith('.png')) {
      return originalName;
    }
    return 'photo.jpg';
  }

  Future<void> _upload({
    required String documentType,
    required String fileName,
    String? filePath,
    List<int>? fileBytes,
    required bool replaceExistingOfSameType,
  }) async {
    setState(() => _busy = true);
    try {
      final repo = ref.read(secretaryStudentRepositoryProvider);
      if (replaceExistingOfSameType) {
        final existing = _documents
            .where((d) => d.documentType.toLowerCase() == documentType.toLowerCase())
            .toList();
        for (final doc in existing) {
          await repo.deleteDocument(doc.id);
        }
      }
      await repo.uploadDocument(
        studentId: widget.studentId,
        documentType: documentType,
        fileName: fileName,
        filePath: filePath,
        fileBytes: fileBytes,
      );
      await _refreshDocuments();
      _toast('$documentType enregistré.');
    } catch (e) {
      _toast(e.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _deleteDocument(StudentDocument doc) async {
    try {
      await _ensureCanEdit();
    } catch (_) {
      return;
    }
    if (!mounted) return;

    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Supprimer le document ?'),
        content: Text('${doc.documentType}\n${doc.fileName}'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Annuler')),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            style: FilledButton.styleFrom(backgroundColor: ErpColors.danger),
            child: const Text('Supprimer'),
          ),
        ],
      ),
    );
    if (ok != true) return;

    setState(() => _busy = true);
    try {
      await ref.read(secretaryStudentRepositoryProvider).deleteDocument(doc.id);
      await _refreshDocuments();
      _toast('Document supprimé.');
    } catch (e) {
      _toast(e.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _showAddDocumentSheet() async {
    try {
      await _ensureCanEdit();
    } catch (_) {
      return;
    }
    if (!mounted) return;

    await showModalBottomSheet<void>(
      context: context,
      builder: (context) => SafeArea(
        child: ListView(
          shrinkWrap: true,
          children: [
            const Padding(
              padding: EdgeInsets.fromLTRB(16, 16, 16, 8),
              child: Text(
                'Ajouter / remplacer un document',
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 16),
              ),
            ),
            ...enrollmentDocumentTypes.map(
              (type) => ListTile(
                leading: Icon(type == 'Photo' ? Icons.photo_camera : Icons.attach_file),
                title: Text(type),
                subtitle: type == 'Photo' && _documents.any((d) => d.isPhoto)
                    ? const Text('Remplacera la photo existante')
                    : null,
                onTap: () {
                  Navigator.pop(context);
                  _addOrReplaceDocument(type);
                },
              ),
            ),
          ],
        ),
      ),
    );
  }

  void _toast(String message) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    final canEdit = ref.watch(writePolicyProvider).canMutateBusinessData;
    final student = _profile?.student;

    return Scaffold(
      backgroundColor: ErpColors.pageBackground,
      appBar: AppBar(
        title: Text(student?.fullName ?? 'Dossier élève'),
        backgroundColor: Colors.white,
        foregroundColor: ErpColors.navy,
        elevation: 0,
        actions: [
          IconButton(
            tooltip: 'Actualiser',
            onPressed: _loading || _busy ? null : _load,
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      floatingActionButton: canEdit
          ? FloatingActionButton.extended(
              onPressed: _busy ? null : _showAddDocumentSheet,
              icon: const Icon(Icons.attach_file),
              label: const Text('Documents'),
            )
          : null,
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(
                  child: Padding(
                    padding: const EdgeInsets.all(24),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(_error!, textAlign: TextAlign.center, style: const TextStyle(color: ErpColors.danger)),
                        const SizedBox(height: 16),
                        FilledButton(onPressed: _load, child: const Text('Réessayer')),
                      ],
                    ),
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _load,
                  child: ListView(
                    padding: const EdgeInsets.fromLTRB(16, 12, 16, 100),
                    children: [
                      if (!canEdit)
                        Container(
                          margin: const EdgeInsets.only(bottom: 12),
                          padding: const EdgeInsets.all(12),
                          decoration: BoxDecoration(
                            color: ErpColors.warning.withValues(alpha: 0.12),
                            borderRadius: BorderRadius.circular(12),
                          ),
                          child: Text(
                            ref.watch(writePolicyProvider).readOnlyHint,
                            style: const TextStyle(fontSize: 13),
                          ),
                        ),
                      if (_busy) const LinearProgressIndicator(minHeight: 2),
                      if (student != null) _IdentityCard(student: student, formatDate: _formatDate),
                      const SizedBox(height: 16),
                      _SectionTitle(title: 'Scolarité', count: _profile?.enrollments.length ?? 0),
                      const SizedBox(height: 8),
                      if ((_profile?.enrollments ?? []).isEmpty)
                        const _EmptyHint('Aucune inscription trouvée.')
                      else
                        ..._profile!.enrollments.map(
                          (e) => Padding(
                            padding: const EdgeInsets.only(bottom: 8),
                            child: _EnrollmentTile(enrollment: e, formatDate: _formatDate),
                          ),
                        ),
                      const SizedBox(height: 16),
                      Row(
                        children: [
                          Expanded(
                            child: _SectionTitle(title: 'Documents joints', count: _documents.length),
                          ),
                          if (canEdit)
                            TextButton.icon(
                              onPressed: _busy ? null : _showAddDocumentSheet,
                              icon: const Icon(Icons.add),
                              label: const Text('Ajouter'),
                            ),
                        ],
                      ),
                      const SizedBox(height: 8),
                      if (_documents.isEmpty)
                        const _EmptyHint('Aucun document. Ajoutez une photo ou un fichier.')
                      else
                        ..._documents.map(
                          (doc) => Padding(
                            padding: const EdgeInsets.only(bottom: 8),
                            child: _DocumentTile(
                              document: doc,
                              canEdit: canEdit,
                              sizeLabel: _formatBytes(doc.fileSizeBytes),
                              dateLabel: DateFormat('dd/MM/yyyy HH:mm').format(doc.createdAt.toLocal()),
                              onReplacePhoto: doc.isPhoto && canEdit
                                  ? () => _addOrReplaceDocument('Photo')
                                  : null,
                              onDelete: canEdit ? () => _deleteDocument(doc) : null,
                            ),
                          ),
                        ),
                    ],
                  ),
                ),
    );
  }
}

class _SectionTitle extends StatelessWidget {
  const _SectionTitle({required this.title, required this.count});

  final String title;
  final int count;

  @override
  Widget build(BuildContext context) {
    return Text(
      '$title ($count)',
      style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 16, color: ErpColors.navy),
    );
  }
}

class _EmptyHint extends StatelessWidget {
  const _EmptyHint(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: ErpColors.border),
      ),
      child: Text(text, style: const TextStyle(color: ErpColors.textSecondary)),
    );
  }
}

class _IdentityCard extends StatelessWidget {
  const _IdentityCard({required this.student, required this.formatDate});

  final StudentSummary student;
  final String Function(String) formatDate;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: ErpColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              CircleAvatar(
                radius: 28,
                backgroundColor: ErpColors.primary.withValues(alpha: 0.12),
                child: Text(
                  student.lastName.isNotEmpty ? student.lastName[0].toUpperCase() : '?',
                  style: const TextStyle(
                    color: ErpColors.primary,
                    fontWeight: FontWeight.w800,
                    fontSize: 22,
                  ),
                ),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(student.fullName, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 18)),
                    const SizedBox(height: 4),
                    Text(student.registrationNumber, style: const TextStyle(color: ErpColors.textSecondary)),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          _InfoRow(label: 'Genre', value: student.genderLabel),
          _InfoRow(label: 'Naissance', value: formatDate(student.dateOfBirth)),
          if (student.phone != null && student.phone!.isNotEmpty)
            _InfoRow(label: 'Téléphone', value: student.phone!),
          if (student.email != null && student.email!.isNotEmpty)
            _InfoRow(label: 'Email', value: student.email!),
          if (student.currentYearClassName != null && student.currentYearClassName!.isNotEmpty)
            _InfoRow(label: 'Classe actuelle', value: student.currentYearClassName!),
        ],
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 110,
            child: Text(label, style: const TextStyle(color: ErpColors.textSecondary, fontSize: 13)),
          ),
          Expanded(child: Text(value, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13))),
        ],
      ),
    );
  }
}

class _EnrollmentTile extends StatelessWidget {
  const _EnrollmentTile({required this.enrollment, required this.formatDate});

  final StudentEnrollmentHistory enrollment;
  final String Function(String) formatDate;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: ErpColors.border),
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(enrollment.classDisplayName, style: const TextStyle(fontWeight: FontWeight.w700)),
                const SizedBox(height: 2),
                Text(
                  enrollment.academicYearLabel,
                  style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary),
                ),
                Text(
                  'Inscrit le ${formatDate(enrollment.enrollmentDate)}',
                  style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary),
                ),
              ],
            ),
          ),
          if (enrollment.isCurrentYear)
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              decoration: BoxDecoration(
                color: ErpColors.success.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(8),
              ),
              child: const Text(
                'Année en cours',
                style: TextStyle(fontSize: 11, color: ErpColors.success, fontWeight: FontWeight.w700),
              ),
            ),
        ],
      ),
    );
  }
}

class _DocumentTile extends StatelessWidget {
  const _DocumentTile({
    required this.document,
    required this.canEdit,
    required this.sizeLabel,
    required this.dateLabel,
    this.onReplacePhoto,
    this.onDelete,
  });

  final StudentDocument document;
  final bool canEdit;
  final String sizeLabel;
  final String dateLabel;
  final VoidCallback? onReplacePhoto;
  final VoidCallback? onDelete;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: ErpColors.border),
      ),
      child: Row(
        children: [
          Container(
            width: 42,
            height: 42,
            decoration: BoxDecoration(
              color: ErpColors.primary.withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(10),
            ),
            child: Icon(
              document.isPhoto ? Icons.photo : Icons.description_outlined,
              color: ErpColors.primary,
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(document.documentType, style: const TextStyle(fontWeight: FontWeight.w700)),
                Text(document.fileName, style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary)),
                Text(
                  '$sizeLabel · $dateLabel',
                  style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary),
                ),
              ],
            ),
          ),
          if (canEdit) ...[
            if (onReplacePhoto != null)
              IconButton(
                tooltip: 'Remplacer la photo',
                onPressed: onReplacePhoto,
                icon: const Icon(Icons.photo_camera_outlined),
              ),
            IconButton(
              tooltip: 'Supprimer',
              onPressed: onDelete,
              icon: const Icon(Icons.delete_outline, color: ErpColors.danger),
            ),
          ],
        ],
      ),
    );
  }
}

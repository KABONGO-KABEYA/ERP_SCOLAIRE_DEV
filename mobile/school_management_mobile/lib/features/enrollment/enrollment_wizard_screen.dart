import 'dart:io';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:image_picker/image_picker.dart';
import 'package:intl/intl.dart';

import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';
import 'enrollment_repository.dart';
import 'geography_repository.dart';
import 'models/enrollment_models.dart';
import 'widgets/address_form.dart';

class _PendingEnrollmentFile {
  const _PendingEnrollmentFile({
    required this.documentType,
    required this.fileName,
    this.filePath,
    this.fileBytes,
    this.localPreviewPath,
  });

  final String documentType;
  final String fileName;
  final String? filePath;
  final List<int>? fileBytes;
  final String? localPreviewPath;
}

enum _GuardianApplyTarget { father, mother, contact1, contact2 }

class EnrollmentWizardScreen extends ConsumerStatefulWidget {
  const EnrollmentWizardScreen({super.key, required this.isReinscription});

  final bool isReinscription;

  @override
  ConsumerState<EnrollmentWizardScreen> createState() => _EnrollmentWizardScreenState();
}

class _EnrollmentWizardScreenState extends ConsumerState<EnrollmentWizardScreen> {
  static const _stepTitles = [
    'Identité',
    'Scolarité',
    'Responsables',
    'Santé',
    'Documents',
    'Validation',
  ];

  late final EnrollmentRepository _repo;
  late final GeographyRepository _geoRepo;
  late final AddressEditorState _studentAddress;
  late final AddressEditorState _fatherAddress;
  late final AddressEditorState _motherAddress;
  late final AddressEditorState _contact1Address;
  late final AddressEditorState _contact2Address;

  bool _reposReady = false;
  int _step = 0;
  bool _busy = false;
  String? _error;
  String? _status;

  EnrollmentPrerequisites? _prerequisites;
  EnrollmentStructureOptions? _structure;
  ClassCapacity? _capacity;

  // Identity
  String? _existingStudentId;
  String _registrationNumber = '';
  final _lastName = TextEditingController();
  final _firstName = TextEditingController();
  final _middleName = TextEditingController();
  int? _gender;
  DateTime? _dateOfBirth;
  final _placeOfBirth = TextEditingController();
  final _nationality = TextEditingController(text: 'Congolaise');
  final _language = TextEditingController();
  final _religion = TextEditingController();
  String? _photoPath;
  String? _localPhotoPath;
  final ImagePicker _imagePicker = ImagePicker();

  // Search
  final _studentSearch = TextEditingController();
  List<EnrollmentStudentSearchResult> _studentResults = [];
  EnrollmentStudentSearchResult? _selectedStudent;
  int? _reinscriptionMinClassLevel;

  // Scolarité
  String? _selectedSectionId;
  EnrollmentClassOption? _selectedClass;
  DateTime _enrollmentDate = DateTime.now();
  int _registrationKind = 1;
  final _previousSchool = TextEditingController();
  final _permanentNumber = TextEditingController();

  // Father
  final _fatherLastName = TextEditingController();
  final _fatherFirstName = TextEditingController();
  final _fatherPhone = TextEditingController();
  final _fatherEmail = TextEditingController();
  final _fatherProfession = TextEditingController();
  bool _fatherSameAddress = true;
  String? _fatherExistingGuardianId;

  // Mother
  final _motherLastName = TextEditingController();
  final _motherFirstName = TextEditingController();
  final _motherPhone = TextEditingController();
  final _motherEmail = TextEditingController();
  final _motherProfession = TextEditingController();
  bool _motherSameAddress = true;
  String? _motherExistingGuardianId;

  // Personnes à contacter
  final _guardianSearch = TextEditingController();
  List<EnrollmentGuardianSearchResult> _guardianResults = [];
  bool _guardianSearchEmpty = false;

  final _contact1LastName = TextEditingController();
  final _contact1FirstName = TextEditingController();
  final _contact1Phone = TextEditingController();
  final _contact1Email = TextEditingController();
  final _contact1Relationship = TextEditingController();
  bool _contact1SameAddress = true;
  int? _contact1Gender;
  String? _contact1ExistingGuardianId;

  final _contact2LastName = TextEditingController();
  final _contact2FirstName = TextEditingController();
  final _contact2Phone = TextEditingController();
  final _contact2Email = TextEditingController();
  final _contact2Relationship = TextEditingController();
  bool _contact2SameAddress = true;
  int? _contact2Gender;
  String? _contact2ExistingGuardianId;

  // Medical
  final _bloodGroup = TextEditingController();
  final _allergies = TextEditingController();
  final _chronicDiseases = TextEditingController();
  final _treatment = TextEditingController();
  final _doctorName = TextEditingController();
  final _medicalCenter = TextEditingController();
  final _disability = TextEditingController();
  final _medicalObservations = TextEditingController();
  bool _medicalEmergency = false;

  // Documents (fichiers locaux en attente + chemins serveur déjà enregistrés)
  final Map<String, _PendingEnrollmentFile> _pendingFiles = {};
  final Map<String, EnrollmentDocumentStatus> _documents = {};

  bool _confirmAccuracy = false;
  CompleteEnrollmentResult? _result;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (!_reposReady) {
      final client = ref.read(apiClientProvider);
      _repo = EnrollmentRepository(client);
      _geoRepo = GeographyRepository(client);
      _studentAddress = AddressEditorState(_geoRepo);
      _fatherAddress = AddressEditorState(_geoRepo);
      _motherAddress = AddressEditorState(_geoRepo);
      _contact1Address = AddressEditorState(_geoRepo);
      _contact2Address = AddressEditorState(_geoRepo);
      _reposReady = true;
      _bootstrap();
    }
  }

  @override
  void initState() {
    super.initState();
    if (widget.isReinscription) {
      _registrationKind = 2;
    }
  }

  Future<void> _bootstrap() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      _prerequisites = await _repo.getPrerequisites();
      if (_prerequisites!.isReady) {
        await _loadStructure(force: true);
        await _studentAddress.initialize();
        await _fatherAddress.initialize();
        await _motherAddress.initialize();
        await _contact1Address.initialize();
        await _contact2Address.initialize();
        if (!widget.isReinscription) {
          _registrationNumber = await _repo.generateRegistrationNumber();
        }
      }
    } catch (e) {
      _error = e.toString();
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  void dispose() {
    _lastName.dispose();
    _firstName.dispose();
    _middleName.dispose();
    _placeOfBirth.dispose();
    _nationality.dispose();
    _language.dispose();
    _religion.dispose();
    _studentSearch.dispose();
    _previousSchool.dispose();
    _permanentNumber.dispose();
    _fatherLastName.dispose();
    _fatherFirstName.dispose();
    _fatherPhone.dispose();
    _fatherEmail.dispose();
    _fatherProfession.dispose();
    _motherLastName.dispose();
    _motherFirstName.dispose();
    _motherPhone.dispose();
    _motherEmail.dispose();
    _motherProfession.dispose();
    _guardianSearch.dispose();
    _contact1LastName.dispose();
    _contact1FirstName.dispose();
    _contact1Phone.dispose();
    _contact1Email.dispose();
    _contact1Relationship.dispose();
    _contact2LastName.dispose();
    _contact2FirstName.dispose();
    _contact2Phone.dispose();
    _contact2Email.dispose();
    _contact2Relationship.dispose();
    _bloodGroup.dispose();
    _allergies.dispose();
    _chronicDiseases.dispose();
    _treatment.dispose();
    _doctorName.dispose();
    _medicalCenter.dispose();
    _disability.dispose();
    _medicalObservations.dispose();
    super.dispose();
  }

  String get _stepTitle {
    if (_step == 0 && widget.isReinscription) return 'Recherche élève';
    return _stepTitles[_step];
  }

  Future<void> _loadStructure({bool force = false}) async {
    if (!force && _structure != null) return;
    _structure = await _repo.getStructureOptions();
    if (mounted) setState(() {});
  }

  Future<void> _searchStudents() async {
    final q = _studentSearch.text.trim();
    if (q.length < 2) return;
    setState(() => _busy = true);
    try {
      _studentResults = await _repo.searchStudents(q, forReinscription: widget.isReinscription);
    } catch (e) {
      _error = e.toString();
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  void _applyStudent(EnrollmentStudentSearchResult student) {
    _existingStudentId = student.id;
    _registrationNumber = student.registrationNumber;
    _lastName.text = student.lastName;
    _firstName.text = student.firstName;
    _middleName.text = student.middleName ?? '';
    _gender = student.gender;
    _dateOfBirth = DateTime.tryParse(student.dateOfBirth);
    _photoPath = student.photoPath;
    _localPhotoPath = null;
    _reinscriptionMinClassLevel = widget.isReinscription ? student.lastClassLevel : null;
    _selectedClass = null;
    _selectedStudent = student;
    setState(() {});
  }

  Future<void> _onClassChanged(EnrollmentClassOption? cls) async {
    _selectedClass = cls;
    _capacity = null;
    if (cls != null && _structure != null) {
      try {
        _capacity = await _repo.getClassCapacity(cls.classRoomId, _structure!.academicYearId);
      } catch (e) {
        _error = e.toString();
      }
    }
    setState(() {});
  }

  Future<void> _pickDocument(String documentType) async {
    if (documentType == 'Photo') {
      await _showPhotoSourceSheet(documentType);
      return;
    }

    final result = await FilePicker.platform.pickFiles(withData: true);
    if (result == null || result.files.isEmpty) return;
    final file = result.files.first;
    if (file.path == null && (file.bytes == null || file.bytes!.isEmpty)) {
      setState(() => _error = 'Impossible de lire le fichier sélectionné.');
      return;
    }

    _stageDocument(
      documentType: documentType,
      fileName: file.name,
      filePath: file.path,
      fileBytes: file.bytes,
    );
  }

  String _dossierFirstName() {
    final firstName = _firstName.text.trim();
    return firstName.isEmpty ? _lastName.text.trim() : firstName;
  }

  String _academicYearLabel() =>
      _structure?.academicYearLabel ?? _prerequisites?.currentAcademicYearLabel ?? '';

  int _studentAgeAt(DateTime referenceDate) {
    if (_dateOfBirth == null) return 0;
    var age = referenceDate.year - _dateOfBirth!.year;
    final birthdayThisYear = DateTime(referenceDate.year, _dateOfBirth!.month, _dateOfBirth!.day);
    if (birthdayThisYear.isAfter(referenceDate)) age--;
    return age;
  }

  bool _isClassAgeCompatible(EnrollmentClassOption c) {
    if (_dateOfBirth == null) return true;
    if (c.minAge == null && c.maxAge == null) return true;
    final age = _studentAgeAt(_enrollmentDate);
    if (c.minAge != null && age < c.minAge!) return false;
    if (c.maxAge != null && age > c.maxAge!) return false;
    return true;
  }

  void _stageDocument({
    required String documentType,
    required String fileName,
    String? filePath,
    List<int>? fileBytes,
    String? localPreviewPath,
  }) {
    setState(() {
      _pendingFiles[documentType] = _PendingEnrollmentFile(
        documentType: documentType,
        fileName: fileName,
        filePath: filePath,
        fileBytes: fileBytes,
        localPreviewPath: localPreviewPath,
      );
      if (documentType == 'Photo') {
        _localPhotoPath = localPreviewPath ?? filePath;
        _photoPath = null;
      }
      _documents.remove(documentType);
      _status = '$documentType sélectionné — envoi au serveur lors de l\'enregistrement.';
      _error = null;
    });
  }

  Future<void> _uploadPendingFiles() async {
    if (_pendingFiles.isEmpty) return;

    if (_lastName.text.trim().isEmpty) {
      throw StateError('Le nom de l\'élève est requis pour enregistrer les fichiers.');
    }

    if (_registrationNumber.isEmpty) {
      _registrationNumber = await _repo.generateRegistrationNumber();
    }

    if (_structure == null) {
      await _loadStructure(force: true);
    }

    final academicYearLabel = _academicYearLabel();
    if (academicYearLabel.isEmpty) {
      throw StateError('Année scolaire indisponible.');
    }

    for (final pending in _pendingFiles.values) {
      final stored = await _repo.storeFile(
        lastName: _lastName.text.trim(),
        firstName: _dossierFirstName(),
        registrationNumber: _registrationNumber,
        academicYearLabel: academicYearLabel,
        documentType: pending.documentType,
        fileName: pending.fileName,
        filePath: pending.filePath,
        fileBytes: pending.fileBytes,
      );
      _documents[pending.documentType] = EnrollmentDocumentStatus(
        documentType: pending.documentType,
        status: 'Complet',
        fileName: stored.fileName,
        storagePath: stored.storagePath,
        fileSizeBytes: stored.fileSizeBytes,
      );
      if (pending.documentType == 'Photo') {
        _photoPath = stored.storagePath;
      }
    }

    _pendingFiles.clear();
  }

  String _documentSubtitle(String documentType) {
    final pending = _pendingFiles[documentType];
    if (pending != null) {
      return '${pending.fileName} — en attente d\'enregistrement';
    }

    final doc = _documents[documentType];
    if (doc?.storagePath != null) {
      return '${doc!.fileName ?? documentType} — dossier partagé';
    }
    if (doc?.fileName != null) {
      return doc!.fileName!;
    }
    return 'Facultatif — non fourni';
  }

  Future<void> _capturePhoto(String documentType) async {
    final photo = await _imagePicker.pickImage(
      source: ImageSource.camera,
      preferredCameraDevice: CameraDevice.front,
      maxWidth: 1200,
      imageQuality: 85,
    );
    if (photo == null) return;

    _stageDocument(
      documentType: documentType,
      fileName: _photoFileName(photo.name),
      filePath: photo.path,
      localPreviewPath: photo.path,
    );
  }

  Future<void> _pickPhotoFromGallery(String documentType) async {
    final photo = await _imagePicker.pickImage(
      source: ImageSource.gallery,
      maxWidth: 1200,
      imageQuality: 85,
    );
    if (photo == null) return;

    _stageDocument(
      documentType: documentType,
      fileName: _photoFileName(photo.name),
      filePath: photo.path,
      localPreviewPath: photo.path,
    );
  }

  String _photoFileName(String originalName) {
    final lower = originalName.toLowerCase();
    if (lower.endsWith('.jpg') || lower.endsWith('.jpeg') || lower.endsWith('.png')) {
      return originalName;
    }
    return 'photo.jpg';
  }

  Future<void> _showPhotoSourceSheet(String documentType) async {
    await showModalBottomSheet<void>(
      context: context,
      builder: (context) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
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
                  setState(() => _error = 'Impossible de lire l\'image sélectionnée.');
                  return;
                }
                _stageDocument(
                  documentType: documentType,
                  fileName: _photoFileName(file.name),
                  filePath: file.path,
                  fileBytes: file.bytes,
                  localPreviewPath: file.path,
                );
              },
            ),
          ],
        ),
      ),
    );
  }

  void _removePhoto() {
    setState(() {
      _photoPath = null;
      _localPhotoPath = null;
      _pendingFiles.remove('Photo');
      _documents.remove('Photo');
      _status = 'Photo supprimée.';
    });
  }

  Widget _buildPhotoSection() {
    final previewPath = _localPhotoPath;
    final hasPhoto = previewPath != null && File(previewPath).existsSync();

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Photo de l\'élève', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 12),
            Center(
              child: Container(
                width: 120,
                height: 120,
                decoration: BoxDecoration(
                  color: Theme.of(context).colorScheme.surfaceContainerHighest,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: Theme.of(context).dividerColor),
                ),
                clipBehavior: Clip.antiAlias,
                child: hasPhoto
                    ? Image.file(File(previewPath), fit: BoxFit.cover)
                    : Icon(Icons.person, size: 56, color: Theme.of(context).colorScheme.outline),
              ),
            ),
            if (_pendingFiles.containsKey('Photo')) ...[
              const SizedBox(height: 8),
              Text(
                'En attente d\'enregistrement',
                style: Theme.of(context).textTheme.bodySmall?.copyWith(color: ErpColors.warning),
                textAlign: TextAlign.center,
              ),
            ] else if (_photoPath != null) ...[
              const SizedBox(height: 8),
              Text(
                'Photo existante sur le serveur',
                style: Theme.of(context).textTheme.bodySmall,
                textAlign: TextAlign.center,
              ),
            ],
            const SizedBox(height: 12),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              alignment: WrapAlignment.center,
              children: [
                OutlinedButton.icon(
                  onPressed: _busy ? null : () => _capturePhoto('Photo'),
                  icon: const Icon(Icons.photo_camera),
                  label: const Text('Prendre'),
                ),
                OutlinedButton.icon(
                  onPressed: _busy ? null : () => _pickPhotoFromGallery('Photo'),
                  icon: const Icon(Icons.photo_library),
                  label: const Text('Galerie'),
                ),
                if (_photoPath != null || _pendingFiles.containsKey('Photo'))
                  TextButton.icon(
                    onPressed: _busy ? null : _removePhoto,
                    icon: const Icon(Icons.delete_outline),
                    label: const Text('Supprimer'),
                  ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _searchGuardians() async {
    final q = _guardianSearch.text.trim();
    if (q.length < 2) return;
    setState(() => _busy = true);
    try {
      _guardianResults = await _repo.searchGuardians(q);
      _guardianSearchEmpty = _guardianResults.isEmpty;
      _error = null;
    } catch (e) {
      _error = e.toString();
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  bool _isContactFilled({
    required TextEditingController lastName,
    required TextEditingController firstName,
    required TextEditingController phone,
    required TextEditingController email,
  }) =>
      lastName.text.trim().isNotEmpty ||
      firstName.text.trim().isNotEmpty ||
      phone.text.trim().isNotEmpty ||
      email.text.trim().isNotEmpty;

  void _applyGuardian(EnrollmentGuardianSearchResult guardian, _GuardianApplyTarget target) {
    if (target == _GuardianApplyTarget.father && guardian.gender == 2) {
      setState(() => _error = 'Impossible d\'appliquer comme père : sexe féminin enregistré.');
      return;
    }
    if (target == _GuardianApplyTarget.mother && guardian.gender == 1) {
      setState(() => _error = 'Impossible d\'appliquer comme mère : sexe masculin enregistré.');
      return;
    }

    setState(() {
      switch (target) {
        case _GuardianApplyTarget.father:
          _fatherExistingGuardianId = guardian.id;
          _fatherLastName.text = guardian.lastName;
          _fatherFirstName.text = guardian.firstName;
          _fatherPhone.text = guardian.phone ?? '';
          _fatherEmail.text = guardian.email ?? '';
          _fatherSameAddress = true;
          _fatherAddress.reset();
          break;
        case _GuardianApplyTarget.mother:
          _motherExistingGuardianId = guardian.id;
          _motherLastName.text = guardian.lastName;
          _motherFirstName.text = guardian.firstName;
          _motherPhone.text = guardian.phone ?? '';
          _motherEmail.text = guardian.email ?? '';
          _motherSameAddress = true;
          _motherAddress.reset();
          break;
        case _GuardianApplyTarget.contact1:
          _contact1ExistingGuardianId = guardian.id;
          _contact1LastName.text = guardian.lastName;
          _contact1FirstName.text = guardian.firstName;
          _contact1Phone.text = guardian.phone ?? '';
          _contact1Email.text = guardian.email ?? '';
          _contact1Gender = guardian.gender;
          _contact1SameAddress = true;
          _contact1Address.reset();
          break;
        case _GuardianApplyTarget.contact2:
          _contact2ExistingGuardianId = guardian.id;
          _contact2LastName.text = guardian.lastName;
          _contact2FirstName.text = guardian.firstName;
          _contact2Phone.text = guardian.phone ?? '';
          _contact2Email.text = guardian.email ?? '';
          _contact2Gender = guardian.gender;
          _contact2SameAddress = true;
          _contact2Address.reset();
          break;
      }
      _status = 'Responsable ${guardian.fullName} appliqué.';
      _error = null;
    });
  }

  String? _validateResponsiblePerson({
    required TextEditingController lastName,
    required TextEditingController firstName,
    required TextEditingController phone,
    required String roleLabel,
  }) {
    if (lastName.text.trim().isEmpty || firstName.text.trim().isEmpty) {
      return 'Renseignez le nom et prénom du/de la $roleLabel.';
    }
    if (phone.text.trim().isEmpty) {
      return 'Le téléphone du/de la $roleLabel est obligatoire.';
    }
    return null;
  }

  String? _validateOptionalContact({
    required TextEditingController lastName,
    required TextEditingController firstName,
    required TextEditingController phone,
    required TextEditingController email,
    required int? gender,
    required bool sameAddress,
    required AddressEditorState address,
    required String roleLabel,
  }) {
    if (!_isContactFilled(
      lastName: lastName,
      firstName: firstName,
      phone: phone,
      email: email,
    )) {
      return null;
    }

    final base = _validateResponsiblePerson(
      lastName: lastName,
      firstName: firstName,
      phone: phone,
      roleLabel: roleLabel,
    );
    if (base != null) return base;
    if (gender == null) return 'Le sexe est obligatoire pour la $roleLabel.';
    if (!sameAddress && !address.toInput().hasContent) {
      return 'Renseignez l\'adresse de la $roleLabel ou cochez « même adresse que l\'élève ».';
    }
    return null;
  }

  void _addGuardianIfFilled(
    List<GuardianInput> guardians, {
    required TextEditingController firstName,
    required TextEditingController lastName,
    required TextEditingController phone,
    required TextEditingController email,
    TextEditingController? profession,
    required String relationship,
    required bool isPrimary,
    required bool canPickup,
    required int? gender,
    required bool sameAddress,
    required AddressEditorState addressEditor,
    String? existingGuardianId,
  }) {
    if (lastName.text.trim().isEmpty && firstName.text.trim().isEmpty) return;

    guardians.add(GuardianInput(
      firstName: firstName.text.trim(),
      lastName: lastName.text.trim(),
      phone: phone.text.trim().isEmpty ? null : phone.text.trim(),
      email: email.text.trim().isEmpty ? null : email.text.trim(),
      profession: profession == null || profession.text.trim().isEmpty ? null : profession.text.trim(),
      relationship: relationship,
      isPrimary: isPrimary,
      canPickup: canPickup,
      gender: gender,
      usesStudentAddress: sameAddress,
      residenceAddress: sameAddress ? null : addressEditor.toInput(),
      existingGuardianId: existingGuardianId,
    ));
  }

  List<GuardianInput> _buildGuardianInputs() {
    final guardians = <GuardianInput>[];
    _addGuardianIfFilled(
      guardians,
      firstName: _fatherFirstName,
      lastName: _fatherLastName,
      phone: _fatherPhone,
      email: _fatherEmail,
      profession: _fatherProfession,
      relationship: 'Père',
      isPrimary: true,
      canPickup: false,
      gender: 1,
      sameAddress: _fatherSameAddress,
      addressEditor: _fatherAddress,
      existingGuardianId: _fatherExistingGuardianId,
    );
    _addGuardianIfFilled(
      guardians,
      firstName: _motherFirstName,
      lastName: _motherLastName,
      phone: _motherPhone,
      email: _motherEmail,
      profession: _motherProfession,
      relationship: 'Mère',
      isPrimary: false,
      canPickup: false,
      gender: 2,
      sameAddress: _motherSameAddress,
      addressEditor: _motherAddress,
      existingGuardianId: _motherExistingGuardianId,
    );
    if (_isContactFilled(
      lastName: _contact1LastName,
      firstName: _contact1FirstName,
      phone: _contact1Phone,
      email: _contact1Email,
    )) {
      _addGuardianIfFilled(
        guardians,
        firstName: _contact1FirstName,
        lastName: _contact1LastName,
        phone: _contact1Phone,
        email: _contact1Email,
        relationship: _contact1Relationship.text.trim().isEmpty
            ? 'Personne à contacter 1'
            : _contact1Relationship.text.trim(),
        isPrimary: false,
        canPickup: false,
        gender: _contact1Gender,
        sameAddress: _contact1SameAddress,
        addressEditor: _contact1Address,
        existingGuardianId: _contact1ExistingGuardianId,
      );
    }
    if (_isContactFilled(
      lastName: _contact2LastName,
      firstName: _contact2FirstName,
      phone: _contact2Phone,
      email: _contact2Email,
    )) {
      _addGuardianIfFilled(
        guardians,
        firstName: _contact2FirstName,
        lastName: _contact2LastName,
        phone: _contact2Phone,
        email: _contact2Email,
        relationship: _contact2Relationship.text.trim().isEmpty
            ? 'Personne à contacter 2'
            : _contact2Relationship.text.trim(),
        isPrimary: false,
        canPickup: true,
        gender: _contact2Gender,
        sameAddress: _contact2SameAddress,
        addressEditor: _contact2Address,
        existingGuardianId: _contact2ExistingGuardianId,
      );
    }
    return guardians;
  }

  Widget _buildGuardianSearchSection() {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Rechercher un responsable existant', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            Row(
              children: [
                Expanded(
                  child: TextField(
                    controller: _guardianSearch,
                    decoration: const InputDecoration(
                      labelText: 'Nom, téléphone, e-mail…',
                      border: OutlineInputBorder(),
                    ),
                    onSubmitted: (_) => _searchGuardians(),
                  ),
                ),
                const SizedBox(width: 8),
                IconButton.filled(
                  onPressed: _busy ? null : _searchGuardians,
                  icon: const Icon(Icons.search),
                ),
              ],
            ),
            if (_guardianSearchEmpty)
              const Padding(
                padding: EdgeInsets.only(top: 8),
                child: Text('Aucun responsable trouvé.'),
              ),
            ..._guardianResults.map(
              (g) => Card(
                margin: const EdgeInsets.only(top: 8),
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Text(g.fullName, style: Theme.of(context).textTheme.titleSmall),
                      if (g.phone != null || g.email != null)
                        Text([g.phone, g.email].whereType<String>().where((s) => s.isNotEmpty).join(' • ')),
                      const SizedBox(height: 8),
                      Wrap(
                        spacing: 8,
                        runSpacing: 4,
                        children: [
                          OutlinedButton(onPressed: () => _applyGuardian(g, _GuardianApplyTarget.father), child: const Text('Père')),
                          OutlinedButton(onPressed: () => _applyGuardian(g, _GuardianApplyTarget.mother), child: const Text('Mère')),
                          OutlinedButton(onPressed: () => _applyGuardian(g, _GuardianApplyTarget.contact1), child: const Text('Contact 1')),
                          OutlinedButton(onPressed: () => _applyGuardian(g, _GuardianApplyTarget.contact2), child: const Text('Contact 2')),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildOptionalContactSection({
    required String title,
    required TextEditingController lastName,
    required TextEditingController firstName,
    required TextEditingController phone,
    required TextEditingController email,
    required TextEditingController relationship,
    required bool sameAddress,
    required ValueChanged<bool> onSameAddressChanged,
    required AddressEditorState addressEditor,
    required int? gender,
    required ValueChanged<int?> onGenderChanged,
  }) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(title, style: Theme.of(context).textTheme.titleMedium),
        _field(lastName, 'Nom'),
        _field(firstName, 'Prénom'),
        _field(phone, 'Téléphone'),
        _field(email, 'E-mail'),
        DropdownButtonFormField<int>(
          value: gender,
          decoration: InputDecoration(
            labelText: _isContactFilled(
              lastName: lastName,
              firstName: firstName,
              phone: phone,
              email: email,
            )
                ? 'Sexe *'
                : 'Sexe',
            border: OutlineInputBorder(borderRadius: BorderRadius.circular(ErpSpacing.inputRadius)),
          ),
          items: const [
            DropdownMenuItem(value: 1, child: Text('Masculin')),
            DropdownMenuItem(value: 2, child: Text('Féminin')),
          ],
          onChanged: onGenderChanged,
        ),
        const SizedBox(height: 12),
        _field(relationship, 'Lien / relation'),
        SwitchListTile(
          title: const Text('Même adresse que l\'élève'),
          value: sameAddress,
          onChanged: onSameAddressChanged,
        ),
        if (!sameAddress) AddressForm(editor: addressEditor),
        const Divider(height: 32),
      ],
    );
  }

  String? _validateCurrentStep() {
    if (_step == 0) {
      if (widget.isReinscription) {
        if (_existingStudentId == null) return 'Sélectionnez un élève à réinscrire.';
        return null;
      }
      if (_lastName.text.trim().isEmpty) return 'Le nom est obligatoire.';
      if (_middleName.text.trim().isEmpty) return 'Le postnom est obligatoire.';
      if (_gender == null) return 'Sélectionnez le sexe.';
      if (_dateOfBirth == null) return 'Indiquez la date de naissance.';
      if (_dateOfBirth!.isAfter(DateTime.now())) return 'Date de naissance invalide.';
      return null;
    }
    if (_step == 1) {
      if (_selectedClass == null) return 'Sélectionnez une classe.';
      if (_reinscriptionMinClassLevel != null && _selectedClass!.level < _reinscriptionMinClassLevel!) {
        return 'La classe sélectionnée est inférieure à la dernière classe de l\'élève.';
      }
      if (_enrollmentDate.isAfter(DateTime.now())) return 'La date d\'inscription ne peut pas être dans le futur.';
      if (_capacity?.isFull == true) return 'Cette classe est complète.';
      return null;
    }
    if (_step == 2) {
      final fatherError = _validateResponsiblePerson(
        lastName: _fatherLastName,
        firstName: _fatherFirstName,
        phone: _fatherPhone,
        roleLabel: 'père',
      );
      if (fatherError != null) return fatherError;
      if (!_fatherSameAddress && !_fatherAddress.toInput().hasContent) {
        return 'Renseignez l\'adresse du père ou cochez « même adresse que l\'élève ».';
      }

      final motherError = _validateResponsiblePerson(
        lastName: _motherLastName,
        firstName: _motherFirstName,
        phone: _motherPhone,
        roleLabel: 'mère',
      );
      if (motherError != null) return motherError;
      if (!_motherSameAddress && !_motherAddress.toInput().hasContent) {
        return 'Renseignez l\'adresse de la mère ou cochez « même adresse que l\'élève ».';
      }

      final contact1Error = _validateOptionalContact(
        lastName: _contact1LastName,
        firstName: _contact1FirstName,
        phone: _contact1Phone,
        email: _contact1Email,
        gender: _contact1Gender,
        sameAddress: _contact1SameAddress,
        address: _contact1Address,
        roleLabel: '1ère personne à contacter',
      );
      if (contact1Error != null) return contact1Error;

      final contact2Error = _validateOptionalContact(
        lastName: _contact2LastName,
        firstName: _contact2FirstName,
        phone: _contact2Phone,
        email: _contact2Email,
        gender: _contact2Gender,
        sameAddress: _contact2SameAddress,
        address: _contact2Address,
        roleLabel: '2ème personne à contacter',
      );
      if (contact2Error != null) return contact2Error;
      return null;
    }
    if (_step == 5 && !_confirmAccuracy) {
      return 'Confirmez l\'exactitude des informations.';
    }
    return null;
  }

  Future<void> _next() async {
    final validation = _validateCurrentStep();
    if (validation != null) {
      setState(() => _error = validation);
      return;
    }
    setState(() => _error = null);

    if (_step == 0 && !widget.isReinscription && _registrationNumber.isEmpty) {
      try {
        _registrationNumber = await _repo.generateRegistrationNumber();
      } catch (e) {
        setState(() => _error = e.toString());
        return;
      }
    }

    if (_step == 0 && _step + 1 == 1) {
      await _loadStructure(force: true);
    }

    if (_step < 5) {
      setState(() => _step++);
    }
  }

  void _prev() {
    if (_step > 0) {
      setState(() {
        _step--;
        _error = null;
      });
    }
  }

  CompleteEnrollmentRequest _buildRequest({required bool confirm}) {
    final studentAddr = _studentAddress.toInput();
    final guardians = _buildGuardianInputs();

    final docs = enrollmentDocumentTypes
        .map((type) => _documents[type] ??
            EnrollmentDocumentStatus(documentType: type, status: 'Manquant'))
        .toList();

    return CompleteEnrollmentRequest(
      existingStudentId: _existingStudentId,
      firstName: _firstName.text.trim(),
      lastName: _lastName.text.trim(),
      middleName: _middleName.text.trim().isEmpty ? null : _middleName.text.trim(),
      gender: _gender!,
      dateOfBirth: DateFormat('yyyy-MM-dd').format(_dateOfBirth!),
      placeOfBirth: _placeOfBirth.text.trim().isEmpty ? null : _placeOfBirth.text.trim(),
      nationality: _nationality.text.trim().isEmpty ? 'Congolaise' : _nationality.text.trim(),
      residenceAddress: studentAddr.hasContent ? studentAddr : null,
      language: _language.text.trim().isEmpty ? null : _language.text.trim(),
      religion: _religion.text.trim().isEmpty ? null : _religion.text.trim(),
      photoPath: _photoPath,
      medical: EnrollmentMedical(
        bloodGroup: _bloodGroup.text.trim().isEmpty ? null : _bloodGroup.text.trim(),
        allergies: _allergies.text.trim().isEmpty ? null : _allergies.text.trim(),
        chronicDiseases: _chronicDiseases.text.trim().isEmpty ? null : _chronicDiseases.text.trim(),
        treatment: _treatment.text.trim().isEmpty ? null : _treatment.text.trim(),
        doctorName: _doctorName.text.trim().isEmpty ? null : _doctorName.text.trim(),
        medicalCenter: _medicalCenter.text.trim().isEmpty ? null : _medicalCenter.text.trim(),
        disability: _disability.text.trim().isEmpty ? null : _disability.text.trim(),
        observations: _medicalObservations.text.trim().isEmpty ? null : _medicalObservations.text.trim(),
        medicalEmergency: _medicalEmergency,
      ),
      scolarite: EnrollmentScolarite(
        sectionId: _selectedClass!.sectionId,
        classRoomId: _selectedClass!.classRoomId,
        pedagogicalClassId: _selectedClass!.pedagogicalClassId,
        enrollmentDate: DateFormat('yyyy-MM-dd').format(_enrollmentDate),
        registrationKind: _registrationKind,
        previousSchool: _previousSchool.text.trim().isEmpty ? null : _previousSchool.text.trim(),
        permanentNumber: _permanentNumber.text.trim().isEmpty ? null : _permanentNumber.text.trim(),
      ),
      guardians: guardians,
      documents: docs,
      confirmAccuracy: confirm,
    );
  }

  Future<void> _submit() async {
    final policy = ref.read(writePolicyProvider);
    if (!policy.canEnrollStudents) {
      setState(() => _error = policy.readOnlyHint);
      return;
    }

    final validation = _validateCurrentStep();
    if (validation != null) {
      setState(() => _error = validation);
      return;
    }

    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await _uploadPendingFiles();
      final request = _buildRequest(confirm: true);
      final check = await _repo.validate(request);
      if (!check.isValid) {
        setState(() {
          _error = check.issues.map((i) => i.message).join('\n');
        });
        return;
      }
      final result = await _repo.complete(request);
      setState(() {
        _result = result;
        _status = result.message;
      });
    } catch (e) {
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_busy && _prerequisites == null) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }

    if (_prerequisites != null && !_prerequisites!.isReady) {
      return Scaffold(
        appBar: AppBar(title: const Text('Inscription')),
        body: ListView(
          padding: const EdgeInsets.all(ErpSpacing.page),
          children: [
            const Icon(Icons.warning_amber, color: ErpColors.warning, size: 48),
            const SizedBox(height: 16),
            Text('Configuration incomplète', style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 12),
            ..._prerequisites!.issues.map(
              (issue) => Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: Text('• ${issue.message}'),
              ),
            ),
          ],
        ),
      );
    }

    if (_result != null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Inscription réussie')),
        body: Padding(
          padding: const EdgeInsets.all(ErpSpacing.page),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Icon(Icons.check_circle, color: ErpColors.success, size: 64),
              const SizedBox(height: 16),
              Text(_result!.studentFullName, style: Theme.of(context).textTheme.headlineMedium),
              Text('Matricule : ${_result!.registrationNumber}'),
              Text('Classe : ${_result!.className}'),
              if (_result!.message.contains('Fiche d\'inscription'))
                Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: Text(
                    _result!.message,
                    style: Theme.of(context).textTheme.bodyMedium,
                  ),
                ),
              const SizedBox(height: 24),
              FilledButton(
                onPressed: () => context.go('/secretary/home'),
                child: const Text('Retour au secrétariat'),
              ),
            ],
          ),
        ),
      );
    }

    return Scaffold(
      appBar: AppBar(
        title: Text(_stepTitle),
        leading: IconButton(
          icon: const Icon(Icons.close),
          onPressed: () => context.pop(),
        ),
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
            child: _EnrollmentStepper(currentStep: _step, titles: _stepTitles),
          ),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: Text(_error!, style: const TextStyle(color: ErpColors.danger)),
            ),
          if (_status != null)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: Text(_status!, style: const TextStyle(color: ErpColors.success)),
            ),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.all(ErpSpacing.page),
              children: [_buildStep()],
            ),
          ),
          SafeArea(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                children: [
                  if (_step > 0)
                    OutlinedButton(
                      onPressed: _busy ? null : _prev,
                      child: const Text('Précédent'),
                    ),
                  const Spacer(),
                  if (_step < 5)
                    FilledButton(
                      onPressed: _busy ? null : _next,
                      child: const Text('Suivant'),
                    )
                  else
                    FilledButton(
                      onPressed: (_busy || !ref.watch(writePolicyProvider).canEnrollStudents)
                          ? null
                          : _submit,
                      child: _busy
                          ? const SizedBox(
                              width: 20,
                              height: 20,
                              child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                            )
                          : const Text('Inscrire l\'élève'),
                    ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildStep() {
    switch (_step) {
      case 0:
        return widget.isReinscription ? _buildSearchStep() : _buildIdentityStep();
      case 1:
        return _buildScolariteStep();
      case 2:
        return _buildGuardiansStep();
      case 3:
        return _buildMedicalStep();
      case 4:
        return _buildDocumentsStep();
      case 5:
        return _buildValidationStep();
      default:
        return const SizedBox.shrink();
    }
  }

  Widget _buildSearchStep() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        TextField(
          controller: _studentSearch,
          decoration: InputDecoration(
            labelText: 'Rechercher un élève',
            suffixIcon: IconButton(icon: const Icon(Icons.search), onPressed: _searchStudents),
          ),
          onSubmitted: (_) => _searchStudents(),
        ),
        const SizedBox(height: 12),
        ..._studentResults.map(
          (s) => Card(
            child: ListTile(
              title: Text(s.fullName),
              subtitle: Text('${s.registrationNumber} • ${s.statusLabel ?? ''}'),
              selected: _selectedStudent?.id == s.id,
              onTap: () => _applyStudent(s),
            ),
          ),
        ),
        if (_selectedStudent != null) ...[
          const SizedBox(height: 16),
          Text('Élève sélectionné : ${_selectedStudent!.fullName}',
              style: Theme.of(context).textTheme.titleMedium),
        ],
      ],
    );
  }

  Widget _buildIdentityStep() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (_registrationNumber.isNotEmpty)
          Text('Matricule : $_registrationNumber', style: Theme.of(context).textTheme.titleMedium),
        const SizedBox(height: 12),
        _field(_lastName, 'Nom *'),
        _field(_middleName, 'Postnom *'),
        _field(_firstName, 'Prénom'),
        DropdownButtonFormField<int>(
          value: _gender,
          decoration: const InputDecoration(labelText: 'Sexe *'),
          items: const [
            DropdownMenuItem(value: 1, child: Text('Masculin')),
            DropdownMenuItem(value: 2, child: Text('Féminin')),
          ],
          onChanged: (v) => setState(() => _gender = v),
        ),
        const SizedBox(height: 12),
        ListTile(
          contentPadding: EdgeInsets.zero,
          title: const Text('Date de naissance *'),
          subtitle: Text(
            _dateOfBirth == null
                ? 'Non définie'
                : DateFormat('dd/MM/yyyy').format(_dateOfBirth!),
          ),
          trailing: const Icon(Icons.calendar_today),
          onTap: () async {
            final picked = await showDatePicker(
              context: context,
              initialDate: _dateOfBirth ?? DateTime(2015),
              firstDate: DateTime(1990),
              lastDate: DateTime.now(),
            );
            if (picked != null) setState(() => _dateOfBirth = picked);
          },
        ),
        _field(_placeOfBirth, 'Lieu de naissance'),
        _field(_nationality, 'Nationalité'),
        const SizedBox(height: 8),
        _buildPhotoSection(),
        const SizedBox(height: 8),
        Text('Adresse', style: Theme.of(context).textTheme.titleMedium),
        AddressForm(editor: _studentAddress),
      ],
    );
  }

  Widget _buildScolariteStep() {
    final activeSectionIds = (_structure?.classes ?? [])
        .where((c) => c.isSelectable)
        .map((c) => c.sectionId)
        .toSet();
    final sections = (_structure?.sections ?? [])
        .where((s) => activeSectionIds.contains(s.id))
        .toList();
    final classes = _structure?.classes
            .where((c) => _selectedSectionId == null || c.sectionId == _selectedSectionId)
            .where((c) => c.isSelectable)
            .where((c) => _reinscriptionMinClassLevel == null || c.level >= _reinscriptionMinClassLevel!)
            .where(_isClassAgeCompatible)
            .toList() ??
        [];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (_structure != null)
          Text('Année : ${_structure!.academicYearLabel}',
              style: Theme.of(context).textTheme.titleMedium),
        if (_reinscriptionMinClassLevel != null) ...[
          const SizedBox(height: 8),
          Text(
            'Réinscription : classes inférieures à la dernière classe de l\'élève masquées.',
            style: Theme.of(context).textTheme.bodySmall?.copyWith(color: ErpColors.warning),
          ),
        ],
        const SizedBox(height: 12),
        DropdownButtonFormField<String>(
          value: sections.any((s) => s.id == _selectedSectionId) ? _selectedSectionId : null,
          decoration: const InputDecoration(labelText: 'Section'),
          items: sections
              .map((s) => DropdownMenuItem(value: s.id, child: Text(s.name)))
              .toList(),
          onChanged: (v) => setState(() {
            _selectedSectionId = v;
            _selectedClass = null;
            _capacity = null;
          }),
        ),
        if (_selectedSectionId != null && classes.isEmpty) ...[
          const SizedBox(height: 8),
          Text(
            'Aucune classe active pour cette section.',
            style: Theme.of(context).textTheme.bodySmall?.copyWith(color: ErpColors.warning),
          ),
        ],
        const SizedBox(height: 12),
        DropdownButtonFormField<EnrollmentClassOption>(
          value: _selectedClass,
          decoration: const InputDecoration(labelText: 'Classe *'),
          isExpanded: true,
          items: classes
              .map((c) => DropdownMenuItem(
                    value: c,
                    child: Text('${c.fullDisplayName} (${c.currentCount}${c.maxCapacity != null ? '/${c.maxCapacity}' : ''})'),
                  ))
              .toList(),
          onChanged: _onClassChanged,
        ),
        if (_capacity != null)
          Padding(
            padding: const EdgeInsets.only(top: 8),
            child: Text(
              _capacity!.isFull
                  ? 'Classe complète'
                  : 'Places restantes : ${_capacity!.remaining}',
              style: TextStyle(color: _capacity!.isFull ? ErpColors.danger : ErpColors.success),
            ),
          ),
        ListTile(
          contentPadding: EdgeInsets.zero,
          title: const Text('Date d\'inscription'),
          subtitle: Text(DateFormat('dd/MM/yyyy').format(_enrollmentDate)),
          trailing: const Icon(Icons.calendar_today),
          onTap: () async {
            final picked = await showDatePicker(
              context: context,
              initialDate: _enrollmentDate,
              firstDate: DateTime(DateTime.now().year - 1),
              lastDate: DateTime.now(),
            );
            if (picked != null) setState(() => _enrollmentDate = picked);
          },
        ),
        _field(_previousSchool, 'École précédente'),
        _field(_permanentNumber, 'Numéro permanent'),
      ],
    );
  }

  Widget _buildGuardiansStep() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _buildGuardianSearchSection(),
        Text('Père', style: Theme.of(context).textTheme.titleMedium),
        _field(_fatherLastName, 'Nom *'),
        _field(_fatherFirstName, 'Prénom *'),
        _field(_fatherPhone, 'Téléphone *'),
        _field(_fatherEmail, 'Email'),
        _field(_fatherProfession, 'Profession'),
        SwitchListTile(
          title: const Text('Même adresse que l\'élève'),
          value: _fatherSameAddress,
          onChanged: (v) => setState(() => _fatherSameAddress = v),
        ),
        if (!_fatherSameAddress) AddressForm(editor: _fatherAddress),
        const Divider(height: 32),
        Text('Mère', style: Theme.of(context).textTheme.titleMedium),
        _field(_motherLastName, 'Nom *'),
        _field(_motherFirstName, 'Prénom *'),
        _field(_motherPhone, 'Téléphone *'),
        _field(_motherEmail, 'Email'),
        _field(_motherProfession, 'Profession'),
        SwitchListTile(
          title: const Text('Même adresse que l\'élève'),
          value: _motherSameAddress,
          onChanged: (v) => setState(() => _motherSameAddress = v),
        ),
        if (!_motherSameAddress) AddressForm(editor: _motherAddress),
        _buildOptionalContactSection(
          title: 'Personne à contacter 1 (facultatif)',
          lastName: _contact1LastName,
          firstName: _contact1FirstName,
          phone: _contact1Phone,
          email: _contact1Email,
          relationship: _contact1Relationship,
          sameAddress: _contact1SameAddress,
          onSameAddressChanged: (v) => setState(() => _contact1SameAddress = v),
          addressEditor: _contact1Address,
          gender: _contact1Gender,
          onGenderChanged: (v) => setState(() => _contact1Gender = v),
        ),
        _buildOptionalContactSection(
          title: 'Personne à contacter 2 (facultatif, autorisation de récupération)',
          lastName: _contact2LastName,
          firstName: _contact2FirstName,
          phone: _contact2Phone,
          email: _contact2Email,
          relationship: _contact2Relationship,
          sameAddress: _contact2SameAddress,
          onSameAddressChanged: (v) => setState(() => _contact2SameAddress = v),
          addressEditor: _contact2Address,
          gender: _contact2Gender,
          onGenderChanged: (v) => setState(() => _contact2Gender = v),
        ),
      ],
    );
  }

  Widget _buildMedicalStep() {
    return Column(
      children: [
        _field(_bloodGroup, 'Groupe sanguin'),
        _field(_allergies, 'Allergies'),
        _field(_chronicDiseases, 'Maladies chroniques'),
        _field(_treatment, 'Traitement en cours'),
        _field(_doctorName, 'Médecin'),
        _field(_medicalCenter, 'Centre médical'),
        _field(_disability, 'Handicap'),
        _field(_medicalObservations, 'Observations'),
        SwitchListTile(
          title: const Text('Urgence médicale'),
          value: _medicalEmergency,
          onChanged: (v) => setState(() => _medicalEmergency = v),
        ),
      ],
    );
  }

  Widget _buildDocumentsStep() {
    return Column(
      children: enrollmentDocumentTypes.map((type) {
        final isPhoto = type == 'Photo';
        return Card(
          child: ListTile(
            title: Text(type),
            subtitle: Text(_documentSubtitle(type)),
            trailing: isPhoto
                ? IconButton(
                    icon: const Icon(Icons.photo_camera),
                    tooltip: 'Photo (caméra ou galerie)',
                    onPressed: _busy ? null : () => _showPhotoSourceSheet(type),
                  )
                : IconButton(
                    icon: const Icon(Icons.upload_file),
                    onPressed: _busy ? null : () => _pickDocument(type),
                  ),
          ),
        );
      }).toList(),
    );
  }

  Widget _buildValidationStep() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text('Récapitulatif', style: Theme.of(context).textTheme.titleLarge),
        const SizedBox(height: 12),
        Text('${_lastName.text} ${_middleName.text} ${_firstName.text}'.trim()),
        Text('Matricule : $_registrationNumber'),
        Text('Classe : ${_selectedClass?.fullDisplayName ?? '—'}'),
        Text('Père : ${_fatherLastName.text} ${_fatherFirstName.text}'),
        Text('Mère : ${_motherLastName.text} ${_motherFirstName.text}'),
        const SizedBox(height: 16),
        CheckboxListTile(
          value: _confirmAccuracy,
          onChanged: (v) => setState(() => _confirmAccuracy = v ?? false),
          title: const Text('Je confirme l\'exactitude des informations saisies'),
          controlAffinity: ListTileControlAffinity.leading,
        ),
      ],
    );
  }

  Widget _field(TextEditingController controller, String label) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: TextFormField(
        controller: controller,
        decoration: InputDecoration(
          labelText: label,
          border: OutlineInputBorder(borderRadius: BorderRadius.circular(ErpSpacing.inputRadius)),
        ),
      ),
    );
  }
}

class _EnrollmentStepper extends StatelessWidget {
  const _EnrollmentStepper({required this.currentStep, required this.titles});

  final int currentStep;
  final List<String> titles;

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(
        children: [
          for (var i = 0; i < titles.length; i++) ...[
            if (i > 0)
              Container(
                width: 16,
                height: 2,
                margin: const EdgeInsets.only(bottom: 18),
                color: i <= currentStep ? ErpColors.primary : ErpColors.border,
              ),
            Column(
              children: [
                Container(
                  width: 28,
                  height: 28,
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    color: i <= currentStep ? ErpColors.primary : Colors.white,
                    border: Border.all(
                      color: i <= currentStep ? ErpColors.primary : ErpColors.border,
                      width: 1.5,
                    ),
                  ),
                  child: Text(
                    '${i + 1}',
                    style: TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
                      color: i <= currentStep ? Colors.white : ErpColors.textSecondary,
                    ),
                  ),
                ),
                const SizedBox(height: 6),
                Text(
                  titles[i],
                  style: TextStyle(
                    fontSize: 10,
                    fontWeight: i == currentStep ? FontWeight.w700 : FontWeight.w500,
                    color: i == currentStep ? ErpColors.primary : ErpColors.textSecondary,
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

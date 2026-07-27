import 'dart:io';
import 'dart:typed_data';

import 'package:archive/archive.dart';
import 'package:open_filex/open_filex.dart';
import 'package:path_provider/path_provider.dart';

import '../../../core/api/api_client.dart';
import '../models/parent_models.dart';

/// Service isolé — n'altère pas [ParentRepository].
class ParentReceiptZipService {
  ParentReceiptZipService(this._api);

  final ApiClient _api;

  Future<File> downloadAllReceiptsZip(List<ParentPayment> payments) async {
    final completed = payments.where((p) => p.isCompleted).toList();
    if (completed.isEmpty) {
      throw StateError('Aucun reçu validé à exporter.');
    }

    final archive = Archive();
    var added = 0;

    for (final payment in completed) {
      try {
        final feeQuery = payment.feeTypeId != null && payment.feeTypeId!.isNotEmpty
            ? '?feeTypeId=${payment.feeTypeId}'
            : '';
        final bytes = await _api.getBytes(
          '/api/v1/parent/payments/${payment.id}/receipt/pdf$feeQuery',
        );
        final safeName = payment.receiptNumber.replaceAll(RegExp(r'[^\w\-]+'), '_');
        final data = Uint8List.fromList(bytes);
        archive.addFile(ArchiveFile('recu-$safeName.pdf', data.length, data));
        added++;
      } catch (_) {
        // Ignore les reçus individuels en échec pour ne pas bloquer l'export.
      }
    }

    if (added == 0) {
      throw StateError('Impossible de télécharger les reçus.');
    }

    final encoded = ZipEncoder().encode(archive);
    if (encoded == null || encoded.isEmpty) {
      throw StateError('Échec de création du fichier ZIP.');
    }

    final dir = await getTemporaryDirectory();
    final stamp = DateTime.now().millisecondsSinceEpoch;
    final file = File('${dir.path}/recus-parent-$stamp.zip');
    await file.writeAsBytes(encoded, flush: true);
    return file;
  }

  Future<void> downloadAndOpenZip(List<ParentPayment> payments) async {
    final file = await downloadAllReceiptsZip(payments);
    await OpenFilex.open(file.path);
  }
}

import 'package:flutter/material.dart';
import 'package:dio/dio.dart';

import 'update_manager.dart';
import 'update_models.dart';

Future<void> showUpdateDialogIfNeeded(
  BuildContext context,
  UpdateCheckOutcome outcome,
  UpdateManager manager,
) async {
  final manifest = outcome.manifest;
  if (manifest == null) return;

  final mandatory = outcome.availability == UpdateAvailability.mandatory;

  await showDialog<void>(
    context: context,
    barrierDismissible: !mandatory,
    builder: (ctx) {
      return PopScope(
        canPop: !mandatory,
        child: _UpdateDialog(
          outcome: outcome,
          manager: manager,
          mandatory: mandatory,
        ),
      );
    },
  );
}

class _UpdateDialog extends StatefulWidget {
  const _UpdateDialog({
    required this.outcome,
    required this.manager,
    required this.mandatory,
  });

  final UpdateCheckOutcome outcome;
  final UpdateManager manager;
  final bool mandatory;

  @override
  State<_UpdateDialog> createState() => _UpdateDialogState();
}

class _UpdateDialogState extends State<_UpdateDialog> {
  bool _downloading = false;
  double _progress = 0;
  String? _error;
  final _cancel = CancelToken();

  @override
  Widget build(BuildContext context) {
    final m = widget.outcome.manifest!;
    return AlertDialog(
      title: Text(widget.mandatory
          ? 'Mise à jour obligatoire'
          : 'Nouvelle version disponible'),
      content: SizedBox(
        width: 360,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Actuelle : ${widget.outcome.currentVersion}'),
            Text('Nouvelle : ${m.latestVersion}'),
            const SizedBox(height: 12),
            ...m.releaseNotes.map((n) => Text('• $n')),
            if (m.size != null) ...[
              const SizedBox(height: 8),
              Text('Taille : ${_formatSize(m.size!)}'),
            ],
            if (_downloading) ...[
              const SizedBox(height: 16),
              LinearProgressIndicator(value: _progress > 0 ? _progress : null),
            ],
            if (_error != null) ...[
              const SizedBox(height: 8),
              Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
            ],
          ],
        ),
      ),
      actions: [
        if (!widget.mandatory && !_downloading)
          TextButton(
            onPressed: () async {
              await widget.manager.snooze(const Duration(hours: 6));
              if (context.mounted) Navigator.of(context).pop();
            },
            child: const Text('Plus tard'),
          ),
        if (_downloading && !widget.mandatory)
          TextButton(
            onPressed: () => _cancel.cancel('Annulé'),
            child: const Text('Annuler'),
          ),
        FilledButton(
          onPressed: _downloading ? null : _startUpdate,
          child: const Text('Mettre à jour'),
        ),
      ],
    );
  }

  Future<void> _startUpdate() async {
    setState(() {
      _downloading = true;
      _error = null;
      _progress = 0;
    });
    try {
      final file = await widget.manager.downloadAndVerify(
        widget.outcome.manifest!,
        cancelToken: _cancel,
        onProgress: (r, t) {
          if (!mounted) return;
          setState(() => _progress = (t == null || t <= 0) ? 0 : r / t);
        },
      );
      await widget.manager.installApk(file);
      if (mounted) Navigator.of(context).pop();
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _downloading = false;
        _error = e.toString().contains('invalide')
            ? 'Le fichier téléchargé est invalide.'
            : e.toString();
      });
    }
  }

  String _formatSize(int bytes) {
    var b = bytes.toDouble();
    const units = ['o', 'Ko', 'Mo', 'Go'];
    var u = 0;
    while (b >= 1024 && u < units.length - 1) {
      b /= 1024;
      u++;
    }
    return '${b.toStringAsFixed(b >= 10 || u == 0 ? 0 : 1)} ${units[u]}';
  }
}

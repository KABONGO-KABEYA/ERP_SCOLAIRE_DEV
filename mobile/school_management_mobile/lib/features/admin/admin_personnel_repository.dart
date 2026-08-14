import '../../core/api/api_client.dart';
import 'admin_personnel_models.dart';

class AdminPersonnelRepository {
  AdminPersonnelRepository(this._api);

  final ApiClient _api;

  Future<List<PersonnelListItem>> listPersonnel({String? search}) {
    final parts = <String>[];
    if (search != null && search.trim().isNotEmpty) {
      parts.add('search=${Uri.encodeQueryComponent(search.trim())}');
    }
    final query = parts.isEmpty ? '' : '?${parts.join('&')}';
    return _api.getList('/api/v1/personnel$query', PersonnelListItem.fromJson);
  }
}

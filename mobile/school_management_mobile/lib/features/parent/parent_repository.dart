import '../../core/api/api_client.dart';
import 'models/parent_models.dart';

class ParentRepository {
  ParentRepository(this._api);

  final ApiClient _api;

  Future<List<ParentChild>> getChildren() => _api.getList(
        '/api/v1/parent/children',
        ParentChild.fromJson,
      );

  Future<List<ParentPayment>> getPayments(String studentId) => _api.getList(
        '/api/v1/parent/children/$studentId/payments',
        ParentPayment.fromJson,
      );

  Future<List<ParentBulletin>> getBulletins(String studentId) => _api.getList(
        '/api/v1/parent/children/$studentId/bulletins',
        ParentBulletin.fromJson,
      );
}

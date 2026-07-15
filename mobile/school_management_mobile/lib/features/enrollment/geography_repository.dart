import 'package:dio/dio.dart';

import '../../core/api/api_client.dart';
import 'models/geography_models.dart';

class GeographyRepository {
  GeographyRepository(this._client);

  final ApiClient _client;

  Future<List<GeographyItem>> getCountries() =>
      _client.getList('/api/v1/geography/countries', GeographyItem.fromJson);

  Future<List<GeographyItem>> getProvinces(String countryId) => _client.getList(
        '/api/v1/geography/provinces?countryId=$countryId',
        GeographyItem.fromJson,
      );

  Future<List<GeographyItem>> getCities(String provinceId) => _client.getList(
        '/api/v1/geography/cities?provinceId=$provinceId',
        GeographyItem.fromJson,
      );

  Future<List<GeographyItem>> getCommunes(String cityId) => _client.getList(
        '/api/v1/geography/communes?cityId=$cityId',
        GeographyItem.fromJson,
      );

  Future<AddressDto?> getAddress(String addressId) async {
    try {
      return await _client.getObject(
        '/api/v1/geography/addresses/$addressId',
        AddressDto.fromJson,
      );
    } on DioException catch (e) {
      if (e.response?.statusCode == 404) return null;
      rethrow;
    }
  }
}

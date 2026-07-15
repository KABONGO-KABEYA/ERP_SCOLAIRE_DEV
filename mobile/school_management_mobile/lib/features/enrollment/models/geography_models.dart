class GeographyItem {
  const GeographyItem({required this.id, required this.code, required this.name});

  final String id;
  final String code;
  final String name;

  factory GeographyItem.fromJson(Map<String, dynamic> json) => GeographyItem(
        id: json['id'] as String,
        code: json['code'] as String,
        name: json['name'] as String,
      );
}

class AddressInput {
  const AddressInput({
    this.countryId,
    this.provinceId,
    this.cityId,
    this.communeId,
    this.neighborhood,
    this.avenue,
    this.houseNumber,
  });

  final String? countryId;
  final String? provinceId;
  final String? cityId;
  final String? communeId;
  final String? neighborhood;
  final String? avenue;
  final String? houseNumber;

  bool get hasContent =>
      countryId != null ||
      provinceId != null ||
      cityId != null ||
      communeId != null ||
      (neighborhood?.trim().isNotEmpty ?? false) ||
      (avenue?.trim().isNotEmpty ?? false) ||
      (houseNumber?.trim().isNotEmpty ?? false);

  Map<String, dynamic> toJson() => {
        if (countryId != null) 'countryId': countryId,
        if (provinceId != null) 'provinceId': provinceId,
        if (cityId != null) 'cityId': cityId,
        if (communeId != null) 'communeId': communeId,
        if (neighborhood?.trim().isNotEmpty ?? false) 'neighborhood': neighborhood!.trim(),
        if (avenue?.trim().isNotEmpty ?? false) 'avenue': avenue!.trim(),
        if (houseNumber?.trim().isNotEmpty ?? false) 'houseNumber': houseNumber!.trim(),
      };

  factory AddressInput.fromJson(Map<String, dynamic> json) => AddressInput(
        countryId: json['countryId'] as String?,
        provinceId: json['provinceId'] as String?,
        cityId: json['cityId'] as String?,
        communeId: json['communeId'] as String?,
        neighborhood: json['neighborhood'] as String?,
        avenue: json['avenue'] as String?,
        houseNumber: json['houseNumber'] as String?,
      );
}

class AddressDto {
  const AddressDto({
    required this.id,
    this.countryId,
    this.provinceId,
    this.cityId,
    this.communeId,
    this.neighborhood,
    this.avenue,
    this.houseNumber,
  });

  final String id;
  final String? countryId;
  final String? provinceId;
  final String? cityId;
  final String? communeId;
  final String? neighborhood;
  final String? avenue;
  final String? houseNumber;

  AddressInput toInput() => AddressInput(
        countryId: countryId,
        provinceId: provinceId,
        cityId: cityId,
        communeId: communeId,
        neighborhood: neighborhood,
        avenue: avenue,
        houseNumber: houseNumber,
      );

  factory AddressDto.fromJson(Map<String, dynamic> json) => AddressDto(
        id: json['id'] as String,
        countryId: json['countryId'] as String?,
        provinceId: json['provinceId'] as String?,
        cityId: json['cityId'] as String?,
        communeId: json['communeId'] as String?,
        neighborhood: json['neighborhood'] as String?,
        avenue: json['avenue'] as String?,
        houseNumber: json['houseNumber'] as String?,
      );
}

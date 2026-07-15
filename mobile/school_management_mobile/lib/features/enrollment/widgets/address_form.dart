import 'package:flutter/material.dart';

import '../../../core/theme/erp_theme.dart';
import '../geography_repository.dart';
import '../models/geography_models.dart';

/// État d'une adresse avec cascades Pays → Province → Ville → Commune.
class AddressEditorState extends ChangeNotifier {
  AddressEditorState(this._repo);

  final GeographyRepository _repo;

  List<GeographyItem> countries = [];
  List<GeographyItem> provinces = [];
  List<GeographyItem> cities = [];
  List<GeographyItem> communes = [];

  GeographyItem? selectedCountry;
  GeographyItem? selectedProvince;
  GeographyItem? selectedCity;
  GeographyItem? selectedCommune;

  String neighborhood = '';
  String avenue = '';
  String houseNumber = '';

  bool _loading = false;
  bool _initialized = false;
  bool _cascadeLock = false;

  bool get isLoading => _loading;

  Future<void> initialize({AddressInput? initial}) async {
    if (!_initialized) {
      _loading = true;
      notifyListeners();
      countries = await _repo.getCountries();
      _initialized = true;
      _loading = false;
      notifyListeners();
    }

    if (initial != null) {
      await loadFromInput(initial);
    } else {
      final rdc = countries.where((c) => c.code == 'RDC').firstOrNull;
      if (rdc != null && selectedCountry == null) {
        selectedCountry = rdc;
        await _loadProvinces();
        notifyListeners();
      }
    }
  }

  Future<void> loadFromInput(AddressInput input) async {
    _cascadeLock = true;
    selectedCountry = input.countryId != null
        ? countries.where((c) => c.id == input.countryId).firstOrNull
        : null;
    await _loadProvinces();
    selectedProvince = input.provinceId != null
        ? provinces.where((p) => p.id == input.provinceId).firstOrNull
        : null;
    await _loadCities();
    selectedCity =
        input.cityId != null ? cities.where((c) => c.id == input.cityId).firstOrNull : null;
    await _loadCommunes();
    selectedCommune = input.communeId != null
        ? communes.where((c) => c.id == input.communeId).firstOrNull
        : null;
    neighborhood = input.neighborhood ?? '';
    avenue = input.avenue ?? '';
    houseNumber = input.houseNumber ?? '';
    _cascadeLock = false;
    notifyListeners();
  }

  void reset() {
    selectedCountry = null;
    selectedProvince = null;
    selectedCity = null;
    selectedCommune = null;
    provinces = [];
    cities = [];
    communes = [];
    neighborhood = '';
    avenue = '';
    houseNumber = '';
    notifyListeners();
  }

  AddressInput toInput() => AddressInput(
        countryId: selectedCountry?.id,
        provinceId: selectedProvince?.id,
        cityId: selectedCity?.id,
        communeId: selectedCommune?.id,
        neighborhood: neighborhood.trim().isEmpty ? null : neighborhood.trim(),
        avenue: avenue.trim().isEmpty ? null : avenue.trim(),
        houseNumber: houseNumber.trim().isEmpty ? null : houseNumber.trim(),
      );

  Future<void> onCountryChanged(GeographyItem? value) async {
    if (_cascadeLock) return;
    selectedCountry = value;
    selectedProvince = null;
    selectedCity = null;
    selectedCommune = null;
    await _loadProvinces();
    notifyListeners();
  }

  Future<void> onProvinceChanged(GeographyItem? value) async {
    if (_cascadeLock) return;
    selectedProvince = value;
    selectedCity = null;
    selectedCommune = null;
    await _loadCities();
    notifyListeners();
  }

  Future<void> onCityChanged(GeographyItem? value) async {
    if (_cascadeLock) return;
    selectedCity = value;
    selectedCommune = null;
    await _loadCommunes();
    notifyListeners();
  }

  void onCommuneChanged(GeographyItem? value) {
    selectedCommune = value;
    notifyListeners();
  }

  Future<void> _loadProvinces() async {
    provinces = [];
    cities = [];
    communes = [];
    if (selectedCountry == null) return;
    provinces = await _repo.getProvinces(selectedCountry!.id);
  }

  Future<void> _loadCities() async {
    cities = [];
    communes = [];
    if (selectedProvince == null) return;
    cities = await _repo.getCities(selectedProvince!.id);
  }

  Future<void> _loadCommunes() async {
    communes = [];
    if (selectedCity == null) return;
    communes = await _repo.getCommunes(selectedCity!.id);
  }
}

class AddressForm extends StatelessWidget {
  const AddressForm({super.key, required this.editor});

  final AddressEditorState editor;

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: editor,
      builder: (context, _) {
        if (editor.isLoading) {
          return const Padding(
            padding: EdgeInsets.all(16),
            child: Center(child: CircularProgressIndicator()),
          );
        }

        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            _dropdown(
              label: 'Pays',
              value: editor.selectedCountry,
              items: editor.countries,
              onChanged: editor.onCountryChanged,
            ),
            _dropdown(
              label: 'Province',
              value: editor.selectedProvince,
              items: editor.provinces,
              onChanged: editor.onProvinceChanged,
            ),
            _dropdown(
              label: 'Ville',
              value: editor.selectedCity,
              items: editor.cities,
              onChanged: editor.onCityChanged,
            ),
            _dropdown(
              label: 'Commune',
              value: editor.selectedCommune,
              items: editor.communes,
              onChanged: editor.onCommuneChanged,
            ),
            _field(
              label: 'Quartier',
              value: editor.neighborhood,
              onChanged: (v) {
                editor.neighborhood = v;
                editor.notifyListeners();
              },
            ),
            _field(
              label: 'Avenue',
              value: editor.avenue,
              onChanged: (v) {
                editor.avenue = v;
                editor.notifyListeners();
              },
            ),
            _field(
              label: 'N° maison',
              value: editor.houseNumber,
              onChanged: (v) {
                editor.houseNumber = v;
                editor.notifyListeners();
              },
            ),
          ],
        );
      },
    );
  }

  Widget _dropdown({
    required String label,
    required GeographyItem? value,
    required List<GeographyItem> items,
    required ValueChanged<GeographyItem?> onChanged,
  }) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: DropdownButtonFormField<GeographyItem>(
        value: value != null && items.any((i) => i.id == value.id) ? value : null,
        decoration: InputDecoration(
          labelText: label,
          border: OutlineInputBorder(borderRadius: BorderRadius.circular(ErpSpacing.inputRadius)),
        ),
        isExpanded: true,
        items: items
            .map((item) => DropdownMenuItem(value: item, child: Text(item.name)))
            .toList(),
        onChanged: onChanged,
      ),
    );
  }

  Widget _field({
    required String label,
    required String value,
    required ValueChanged<String> onChanged,
  }) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: TextFormField(
        initialValue: value,
        decoration: InputDecoration(
          labelText: label,
          border: OutlineInputBorder(borderRadius: BorderRadius.circular(ErpSpacing.inputRadius)),
        ),
        onChanged: onChanged,
      ),
    );
  }
}

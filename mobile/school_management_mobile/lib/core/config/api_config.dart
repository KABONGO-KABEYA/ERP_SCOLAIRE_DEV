/// URL de base de l'API (sans slash final).
/// Émulateur Android : https://10.0.2.2:7060
/// Bureau / iOS simulateur : https://localhost:7060
const String apiBaseUrl = String.fromEnvironment(
  'API_BASE_URL',
  defaultValue: 'https://10.0.2.2:7060',
);

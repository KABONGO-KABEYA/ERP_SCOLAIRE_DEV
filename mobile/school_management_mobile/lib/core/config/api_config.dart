/// URL de base de l'API (sans slash final).
/// Émulateur Android : http://10.0.2.2:5041
/// Appareil physique (même réseau) : http://<IP-PC>:5041
const String apiBaseUrl = String.fromEnvironment(
  'API_BASE_URL',
  defaultValue: 'http://10.0.2.2:5041',
);
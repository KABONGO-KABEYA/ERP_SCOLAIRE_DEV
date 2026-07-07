import 'dart:io';

import 'package:dio/dio.dart';
import 'package:dio/io.dart';

void configureDio(Dio dio) {
  (dio.httpClientAdapter as IOHttpClientAdapter).createHttpClient = () {
    final client = HttpClient();
    client.badCertificateCallback = (_, __, ___) => true;
    return client;
  };
}

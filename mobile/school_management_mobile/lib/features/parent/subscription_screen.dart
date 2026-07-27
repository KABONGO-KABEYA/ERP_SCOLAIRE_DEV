import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'premium_subscription/screens/premium_offer_screen.dart';

/// Point d'entrée existant — délègue à l'offre Premium V2.
class ParentSubscriptionScreen extends ConsumerWidget {
  const ParentSubscriptionScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return const PremiumOfferScreen();
  }
}

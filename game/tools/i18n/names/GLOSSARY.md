# Card name translation (Yu-Gi-Oh! card names → Spanish and Arabic)

## Spanish
- Use the **official Spanish TCG name** whenever you know it (e.g. Dark Magician → Mago Oscuro, Blue-Eyes White Dragon →
  Dragón Blanco de Ojos Azules, Pot of Greed → Olla de la Codicia, Monster Reborn → Monstruo Renacido,
  Mirror Force → Fuerza Espejo, Elemental HERO Neos → HÉROE Elemental Neos, Kuriboh → Kuriboh).
- No official name known: translate naturally in the same style. Keep invented proper nouns as they are (Kuriboh,
  Exodia, Jinzo, Obelisk...). Keep "LV5" style level tags, "Token" → "Ficha".

## Arabic (no official names exist: translate the meaning, keep it short and natural)
- Dark Magician → الساحر الأسود · Blue-Eyes White Dragon → تنين العيون الزرقاء الأبيض · Pot of Greed → وعاء الجشع ·
  Monster Reborn → إحياء الوحش · Mirror Force → قوة المرآة · Elemental HERO Neos → البطل العنصري نيوس.
- Proper nouns are written in Arabic letters by sound: Kuriboh → كوريبو, Exodia → إكزوديا, Jinzo → جينزو,
  Obelisk → أوبليسك, Slifer → سلايفر, Ra → رع, Yubel → يوبل.
- Archetype words consistent across cards: HERO → البطل, Elemental HERO → البطل العنصري, Destiny HERO → بطل القدر,
  Harpie → هاربي, Cyber → سايبر, Ojama → أوجاما, Toon → تون, Gravekeeper's → حارس المقابر, Red-Eyes → العيون الحمراء,
  Blue-Eyes → العيون الزرقاء. Keep "LV5"-style tags as written. Western digits only.

## Output
JSON object with the SAME keys as the input batch (n0000…), values = translated names, every key present, valid JSON.

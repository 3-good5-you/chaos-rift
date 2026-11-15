# Card text translation rules (Yu-Gi-Oh! card effects, English → Spanish and Arabic)

## Keep exactly as in English
- Card names inside double quotes: `"Dark Magician"` stays `"Dark Magician"` (quotes included). Also unquoted card names.
- Archetype names in quotes (`"Elemental HERO"`), numbers, `ATK`, `DEF`, `LP`, `FLIP:`, line breaks (`\n`), bullets `●`, brackets.
- Markup-like words the game highlights: keep sentence structure so `target`, `Normal Summoned/Set`, etc. map to the terms below.

## Spanish (official TCG Spanish style)
Normal Summon = Invocación Normal · Special Summon = Invocación Especial · Flip Summon = Invocación por Volteo ·
Tribute = Sacrificio (Tribute Summon = Invocación por Sacrificio) · Set = Colocar · Fusion Summon = Invocación por Fusión ·
Ritual Summon = Invocación por Ritual · Graveyard / GY = Cementerio · Deck = Deck · Extra/Fusion Deck = Deck de Fusión ·
hand = mano · field = Campo · banish = desterrar · destroy = destruir · negate = negar · target = seleccionar como objetivo ·
activate = activar · Chain = Cadena · Spell Card = Carta Mágica · Trap Card = Carta de Trampa · Quick-Play = de Juego Rápido ·
Continuous = Continua · Equip = de Equipo · Field Spell = Mágica de Campo · Counter Trap = Trampa de Contraefecto ·
face-up/face-down = boca arriba/boca abajo · Attack/Defense Position = Posición de Ataque/Defensa ·
Battle Phase = Battle Phase/Fase de Batalla · Main Phase = Fase Principal · End Phase = End Phase/Fase Final (use Fase Final) ·
Standby Phase = Fase de Espera · Draw Phase = Fase de Robo · damage calculation = cálculo de daño · battle damage = daño de batalla ·
Damage Step = Paso de Daño · piercing = daño de penetración · opponent = adversario · you control = controlas ·
once per turn = una vez por turno · Level = Nivel · Type = Tipo · Attribute = Atributo · Token = Ficha · counter = Contador ·
Attributes: DARK OSCURIDAD, LIGHT LUZ, EARTH TIERRA, WATER AGUA, FIRE FUEGO, WIND VIENTO, DIVINE DIVINIDAD ·
Types: Dragon Dragón, Spellcaster Lanzador de Conjuros, Zombie Zombi, Warrior Guerrero, Beast-Warrior Guerrero-Bestia, Beast Bestia,
Winged Beast Bestia Alada, Fiend Demonio, Fairy Hada, Insect Insecto, Dinosaur Dinosaurio, Reptile Reptil, Fish Pez,
Sea Serpent Serpiente Marina, Aqua Aqua, Pyro Piro, Thunder Trueno, Rock Roca, Plant Planta, Machine Máquina, Divine-Beast Bestia Divina.
Flavor text (Normal monsters): natural, evocative Spanish.

## Arabic (Modern Standard Arabic; no official text exists — be consistent)
Normal Summon = استدعاء عادي · Special Summon = استدعاء خاص · Flip Summon = استدعاء بالقلب · Tribute = تضحية ·
Set = وضع مقلوبًا · Fusion Summon = استدعاء دمج · Ritual Summon = استدعاء طقوسي · Graveyard / GY = المقبرة · Deck = المجموعة ·
Fusion/Extra Deck = مجموعة الدمج · hand = اليد · field = الميدان · banish = نفي · destroy = تدمير · negate = إبطال ·
target = استهداف · activate = تفعيل · Chain = سلسلة · Spell Card = بطاقة سحر · Trap Card = بطاقة فخ · Quick-Play = سريعة ·
Continuous = مستمرة · Equip = تجهيز · Field Spell = سحر ميدان · Counter Trap = فخ مضاد · face-up/face-down = مكشوفة/مقلوبة ·
Attack/Defense Position = وضع الهجوم/الدفاع · Battle Phase = مرحلة المعركة · Main Phase = المرحلة الرئيسية ·
End Phase = مرحلة النهاية · Standby Phase = مرحلة الاستعداد · Draw Phase = مرحلة السحب · damage calculation = حساب الضرر ·
battle damage = ضرر المعركة · Damage Step = خطوة الضرر · opponent = الخصم · you control = تتحكم بها ·
once per turn = مرة واحدة في كل دور · Level = المستوى · Type = النوع · Attribute = الخاصية · Token = رمز · counter = عدّاد ·
ATK/DEF/LP stay Latin. Attributes: DARK ظلام, LIGHT نور, EARTH أرض, WATER ماء, FIRE نار, WIND رياح, DIVINE إلهي ·
Types: Dragon تنين, Spellcaster ساحر, Zombie زومبي, Warrior محارب, Beast-Warrior محارب وحشي, Beast وحش بري, Winged Beast وحش مجنح,
Fiend شيطان, Fairy جنية, Insect حشرة, Dinosaur ديناصور, Reptile زاحف, Fish سمكة, Sea Serpent ثعبان بحري, Aqua مائي, Pyro ناري,
Thunder رعد, Rock صخر, Plant نبات, Machine آلة, Divine-Beast وحش إلهي.
Use Western digits (0-9), not Arabic-Indic.

## Output
A JSON object with the SAME keys as the input batch (t0000…), values = translations. Valid JSON, UTF-8, every key present,
no extra keys. Escape inner double quotes as \" and line breaks as \n exactly like the input.

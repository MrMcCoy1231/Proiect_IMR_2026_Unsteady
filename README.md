# Unsteady

**Simulator VR de tremor (Parkinson / tremor esențial) pentru empatie și auto-testare a mișcării.**

Proiect pentru cursul *Introducere în Realitate Mixtă (IMR)*, FII Iași, 2026.

Unsteady este o experiență pentru **Meta Quest 2** cu două moduri:

- **Empatie:** aplicația adaugă un tremor artificial pe mâna virtuală, pe care îl vezi și îl simți prin vibrația controllerului, în timp ce faci sarcini de zi cu zi: torni apă, apeși butoane, scrii. Scopul e să înțelegi cât de greu devin gesturile banale pentru cineva cu tremor.
- **Testează-ți mâna:** un mini-joc care măsoară tremorul real al mâinii tale și îți dă un rezultat orientativ, ușor de înțeles (verde / galben / roșu). *Rezultat orientativ, nu diagnostic medical.*

## Echipa

| Membru | Zonă |
|---|---|
| Alexie Tudor | Tremor + vibrație, integrare, review PR-uri |
| Cavaleru Ștefan | Obiecte apucabile, turnatul apei |
| Vasilica Anca | Tastatura (butoane poke), materiale |
| Schipor Andrei Alexandru | UI: panou de control, meniu de start |

## Stadiu

| Funcționalitate | Stare |
|---|---|
| Bucătărie graybox, la scară reală, cu collidere | ✅ |
| Tremor pe controllere (sinus + zgomot Perlin), mâna și obiectul apucat tremură | ✅ |
| Niveluri: Oprit / Ușor / Moderat / Sever | ✅ |
| Profiluri: Parkinson (~5 Hz, scade când miști mâna) și esențial (~8 Hz, crește la mișcare) | ✅ |
| Vibrația controllerului, sincronizată cu tremorul | ✅ |
| Panou de control în lume (nivel, profil, vibrație) | ✅ |
| Meniu de start: Empatie / Testează-ți mâna | ✅ (al doilea mod arată deocamdată „în curând”) |
| Pahar apucabil + turnatul apei (peste ~60°), pahar țintă care se umple | ✅ |
| Tastatură funcțională: 61 de taste reale (QWERTY), apăsate cu degetul / controllerul | ✅ |
| Sarcina de scris: cuvânt pe ecran, timp + greșeli + nivelul de tremor | ✅ |
| Materiale simple: pereți alb cald, mobilă stejar | ✅ |
| Scrisul pe tablă, ecranul de rezultate, scenariul de detecție, texturi | ⏳ planificat |

## Cum rulezi proiectul

1. Instalează **Unity 6.6 (6000.6.7f1)** din Unity Hub, cu **Android Build Support**. Altă versiune modifică fișierele proiectului.
2. Clonează repo-ul (de exemplu cu GitHub Desktop) și deschide folderul din **Unity Hub → Add project from disk**. Prima deschidere durează câteva minute, cât Unity regenerează `Library/`.
3. Deschide `Assets/Scenes/SampleScene.unity` și apasă **Play**.

**Fără cască:** scena folosește **XR Device Simulator**. Cu mouse-ul și tastatura controlezi capul și controllerele.

**Cu Quest 2 prin Quest Link:** pornește Meta Quest Link pe PC, conectează casca, activează Link din cască și apasă Play în Unity. Înainte de testul pe cască, dezactivează XR Device Simulator din scenă.

### Taste de test (în Play, în Editor)

| Tastă | Acțiune |
|---|---|
| F1 / F2 / F3 / F4 | Tremor Oprit / Ușor / Moderat / Sever |
| F5 | Schimbă profilul (Parkinson ↔ esențial) |
| F6 | Pornește / oprește vibrația |
| **Y** (controller stâng, și în cască) | Nivelul următor: Oprit → Ușor → Moderat → Sever |

Ca să testezi tastatura fără cască: în Play, selectează **Tastatura** și bifează **Test With Pc Keyboard**. Literele tastate pe PC apasă tastele virtuale, iar Enter trece la cuvântul următor.

Intensitățile se reglează live, în Play, din Inspector, pe obiectul **Tremor Manager**: amplitudinea în metri, rotația în grade, frecvența și puterea vibrației.

## Structura proiectului

```
Assets/
  Models/Graybox/        modelele FBX (bucătărie, pahar, tastatură, marker, tablă, țintă)
  Scenes/SampleScene     scena principală (o editează doar Tudor)
  Scenes/Dev/            scenele de test ale fiecăruia
  Scripts/Tremor/        TremorManager (stare: nivel, profil, vibrație)
                         TremorHand (tremorul și vibrația pe fiecare controller)
  Scripts/UI/            TremorPanel (panoul de control), StartMenu (meniul de start)
  Scripts/Tastatura/     TastaturaFunctionala (apăsarea tastelor + sarcina de scris), Tasta
  Prefabs/UI/            PanouTremor, MeniuStart
  Prefabs/Tastatura/     tastatura cu taste reale + ecran (Tastatura_Taste.asset = mesh-urile tastelor)
  Prefabs/Bucatarie/     bucătăria cu materialele aplicate
  Materiale/             Perete_AlbCald, Mobila_Stejar, Tasta
  Prefabs/Pahar/         paharul din care torni
  Prefabs/Pahar_tinta/   paharul care se umple
  Scripts/Pahar/         TurnareApa, PaharTinta
docs/                    propunerea și structura proiectului (PDF)
```

### Cum funcționează tremorul

Poziția controllerului e controlată de cască în fiecare frame, deci nu poate fi mișcată direct. La pornire, `TremorHand` creează sub fiecare controller un obiect gol, `Tremor Pivot`, și mută sub el modelul și interactorii. Apoi mișcă doar pivotul. Astfel tremură mâna, raza și obiectul apucat, iar tracking-ul real rămâne neatins.

> Pe obiectele apucabile, debifați **Smooth Position / Smooth Rotation** din *XR Grab Interactable*. Altfel netezirea „înghite” tremorul.

### Meniul Unsteady din Unity

Uneltele de editor care construiesc automat componentele în scena deschisă:

1. **Adaugă tremorul în scena deschisă:** creează Tremor Manager și pune TremorHand pe controllere.
2. **Creează panoul de control (UI):** construiește PanouTremor deasupra mesei.
3. **Creează meniul de start (UI):** construiește MeniuStart în fața jucătorului și îl leagă de panou.
4. **Integrează tastatura + materialele în SampleScene:** pune tastatura lui Anca și materialele pe bucătărie.
5. **Fă tastatura funcțională (taste reale):** desparte modelul `Tastatura.fbx` în 61 de taste, le pune literele QWERTY și ecranul cu sarcina de scris.
6. **Integrează paharele (Ștefan) în SampleScene:** pune `Pahar` și `Pahar_Tinta` pe masă.

## Cum lucrăm pe Git

Fiecare lucrează pe branch-ul lui (`feature/...`) și trimite munca printr-un **Pull Request** către `main`.

- Închide Unity înainte de **Pull**.
- Comite mereu fișierele `.meta` împreună cu fișierele lor.
- Nu edita `SampleScene`. Lucrează în scena ta din `Scenes/Dev/` și predă munca ca prefab.
- **Nu comite niciodată `LiberationSans SDF - Fallback.asset`.** Unity îl modifică singur la fiecare Play (adaugă literele cu diacritice). Dă-i **Discard changes** în GitHub Desktop.

Planul detaliat al săptămânii, cu tutorialul GitHub, e în documentul de plan al echipei.

## Tehnologii

- Unity 6.6 (URP)
- OpenXR + XR Interaction Toolkit 3.5
- TextMesh Pro
- Blender (graybox și modelare)

## Documente

- **Main Modules** (User Story Map, Journey Map, Wireframing, Asset Creation):
  https://docs.google.com/document/d/1EuX3HJGTSEAaGFsn2P1BvLrRb7iNKADKtk8dG9IKw-g/edit
- [`docs/Unsteady_Propunere_IMR.pdf`](docs/Unsteady_Propunere_IMR.pdf): propunerea proiectului
- [`docs/Unsteady_Structura_v2.pdf`](docs/Unsteady_Structura_v2.pdf): structura proiectului și scenariul de detecție

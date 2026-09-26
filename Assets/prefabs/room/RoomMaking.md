Więc pewnie pytasz jak korzystać z systemu tworzenia pokojów

Prerequisites:

- [ ] upewnij się że masz zainstalowany w unity pakiet ProBuilder
- [ ] utwórz pusty obiekt w scenie, zawij go room-XXX (pierwszy wolny jeśli to normalny pokój, lub od tyłu jeśli to
  pokój typu alcove)
- [ ] w tym obiekcie z przygotowanych częsci które znajdują się w folderu prefabs/room/roomparts utwórz pokój, normalny
  pokój ma co najmniej 2 doorways, alcove ma jeden doorways. Wszystkie ściany mają być skierowane do środka.
- [ ] po zakończeniu, rozpakuje wszystkie prefaby, a ściany typu doorways potraktuj opcją GO FOR MILK z menu
  kontekstowego
- [ ] następnie zaznacz wszystkie elementy które zawierają w sobie probuildermesh (w większości wszystkie poza sockets)
  a nazstępnie wejdź w pasku narzędzi w tools/ProBuilder/object/mergeObjects
- [ ] następnie wchodzimy w obiekt room-XXX który utworzyliśmy, zmieniamy warstwę/layer na room, i w inspektorze dodajemy skrypt roomData.
- [ ] w kolejnym kroku klikamy na 3 kropki przy tym skrypcie, i wybieramy opcję bake bounds. Jeśli domyśle opcje
  powodują że pokój nie cały pokój jest objęty żółtymi bryłami, zmniejsz stopniowo test bounds na 2x2.5x2 a następnie na
  1x2.5x1 aż po baku wszystko jest picuś glancuś
- [ ] kiedy uważasz że skończyłeś zapisz pokój jako prefabs w rooms a następnie dodaj go do mapGeneration w sekcji
  randomRoomPrefabs albo randomAlcovePrefabs
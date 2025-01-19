using System.Text;

namespace Jsonh;

public static class Extensions {
    /* Erroneous code:
     public static Rune? PeekRune(this TextReader TextReader) {
        // Peek char
        int Char = TextReader.Peek();
        if (Char < 0) {
            return null;
        }

        // Surrogate pair
        if (char.IsHighSurrogate((char)Char)) {
            return new Rune((char)Char, (char)TextReader.Peek());
        }
        // BMP character
        else {
            return new Rune((char)Char);
        }
    }*/

}
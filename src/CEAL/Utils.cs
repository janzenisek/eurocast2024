using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEAL.Main {
  public static class Utils {
    public static double NextGaussian_BoxMuller(this Random rnd, double mean = 0.0, double stdDev = 1.0) {
      double u1 = rnd.NextDouble(); // uniform(0,1) random doubles
      double u2 = rnd.NextDouble();
      double rndStdNormal = Math.Cos(2.0 * Math.PI * u1) * Math.Sqrt(-2.0 * Math.Log(u2)); // random normal(0,1)
      return mean + stdDev * rndStdNormal;
    }

    public static double NextGaussian_Polar(this Random rnd) {
      double u1 = 0.0, u2 = 0.0, q = 0.0, p = 0.0;

      do {
        u1 = rnd.NextDouble(-1.0, 1.0);
        u2 = rnd.NextDouble(-1.0, 1.0);
        q = u1 * u1 + u2 * u2;
      } while (q == 0.0 || q > 1.0);

      p = Math.Sqrt(-2 * Math.Log(q) / q);
      return u1 * p;
    }

    public static double NextDouble(this Random rnd, double min, double max) {
      return rnd.NextDouble() * (max - min) + min;
    }

    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source, Random rnd) {
      if (source == null) throw new ArgumentNullException(nameof(source));
      if (rnd == null) throw new ArgumentNullException(nameof(rnd));

      return source.ShuffleIterator(rnd);
    }

    private static IEnumerable<T> ShuffleIterator<T>(this IEnumerable<T> source, Random rnd) {
      var buffer = source.ToList();
      for (int i = 0; i < buffer.Count; i++) {
        int j = rnd.Next(i, buffer.Count);
        yield return buffer[j];

        buffer[j] = buffer[i];
      }
    }
  }
}

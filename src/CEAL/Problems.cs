using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEAL.Main {
  public interface IProblem<T> : ICloneable where T : ICloneable {
    Func<T> Creator { get; }
    Func<T, double> Evaluator { get; }
    Func<double[], int> Selector { get; }
    Action<T, T, T> Crossover { get; }
    Action<T> Mutator { get; }
    Func<T, double, bool> Terminator { get; }
  }

  public abstract class DoubleTypedProblem : IProblem<double[]> {
    public virtual Func<double[]> Creator => creator;
    public abstract Func<double[], double> Evaluator { get; }
    public virtual Func<double[], int> Selector => randomSelector;
    public virtual Action<double[], double[], double[]> Crossover => singlePointCrossover;
    public virtual Action<double[]> Mutator => singlePointMutator;

    public abstract Func<double[], double, bool> Terminator { get; }

    protected int problemSize;
    protected double min;
    protected double max;
    protected Random rnd;
    protected int seed;

    public DoubleTypedProblem() { }

    public DoubleTypedProblem(int problemSize, double min, double max, int seed = -1) {
      this.rnd = seed < 0 ? new Random() : new Random(seed);
      this.problemSize = problemSize;
      this.min = min;
      this.max = max;
      this.seed = seed;
    }

    public abstract object Clone();

    private double[] creator() {
      return Enumerable.Range(1, problemSize).Select(_ => rnd.NextDouble() * (max - min) + min).ToArray();
    }

    private int randomSelector(double[] qualities) {
      return rnd.Next(qualities.Length);
    }

    private void singlePointCrossover(double[] parent1, double[] parent2, double[] candidate) {
      int cut = rnd.Next(candidate.Length);
      Array.Copy(parent1, candidate, cut);
      Array.Copy(parent2, cut, candidate, cut, candidate.Length - cut);
    }

    private void singlePointMutator(double[] candidate) {
      candidate[rnd.Next(candidate.Length)] = rnd.NextDouble() * (max - min) + min;
    }
  }

  public class Rastrigin : DoubleTypedProblem {
    public Rastrigin(int problemSize = 100, double min = -5.12, double max = 5.12, int seed = -1)
      : base(problemSize, min, max, seed) {
    }

    public override Func<double[], double> Evaluator => evaluator;

    public override Func<double[], double, bool> Terminator => terminator;

    public override object Clone() {
      return new Rastrigin(problemSize, min, max, seed);
    }

    private double evaluator(double[] x) {
      return 10.0 * x.Length + x.Sum(xi => xi * xi - 10.0 * Math.Cos(2.0 * Math.PI * xi));
    }

    private bool terminator(double[] x, double y) {
      return x.All(i => i == 0.0) && y == 0.0;
    }
  }

  public class Ackley : DoubleTypedProblem {

    protected double a, b, c;

    public Ackley(int problemSize = 100, double min = -32.768, double max = 32.768, double a = 20.0, double b = 0.2, double c = 2 * Math.PI, int seed = -1)
      : base(problemSize, min, max, seed) {
      this.a = a;
      this.b = b;
      this.c = c;
    }
    public override Func<double[], double> Evaluator => evaluator;

    public override Func<double[], double, bool> Terminator => terminator;

    public override object Clone() {
      return new Ackley(problemSize, seed);
    }

    private double evaluator(double[] x) {
      return -a * Math.Exp(-b * Math.Sqrt(1.0 / x.Length * x.Sum(xi => xi * xi))) - Math.Exp(1.0 / x.Length * x.Sum(xi => Math.Cos(c * xi))) + a + Math.E;
    }

    private bool terminator(double[] x, double y) {
      return x.All(i => i == 0.0) && y == 0.0;
    }
  }

  public class Sphere : DoubleTypedProblem {

    public Sphere(int problemSize = 100, double min = double.MinValue, double max = double.MaxValue, int seed = -1)
      : base(problemSize, min, max, seed) {
    }

    public override Func<double[], double> Evaluator => evaluator;

    public override Func<double[], double, bool> Terminator => terminator;

    public override object Clone() {
      return new Sphere(problemSize, min, max, seed);
    }
    private double evaluator(double[] x) {
      return x.Sum(i => i * i);
    }

    private bool terminator(double[] x, double y) {
      return x.All(i => i == 0.0) && y == 0.0;
    }
  }

  public class Rosenbrock : DoubleTypedProblem {

    public Rosenbrock(int problemSize = 100, double min = double.MinValue, double max = double.MaxValue, int seed = -1)
      : base(problemSize, min, max, seed) {
    }

    public override Func<double[], double> Evaluator => evaluator;

    public override Func<double[], double, bool> Terminator => terminator;

    public override object Clone() {
      return new Sphere(problemSize, min, max, seed);
    }
    private double evaluator(double[] x) {
      return Enumerable.Range(0, x.Length).Sum(i => 100 * ((x[i + 1] - x[i] * x[i]) * (x[i + 1] - x[i] * x[i])) + (1 - x[i]) * (1 - x[i]));
    }

    private bool terminator(double[] x, double y) {
      return x.All(i => i == 1.0) && y == 0.0;
    }
  }

  public class Himmelblau : DoubleTypedProblem {
    public Himmelblau(double min = -5.0, double max = 5.0, int seed = -1)
    : base(2, min, max, seed) {
    }

    public override Func<double[], double> Evaluator => evaluator;

    public override Func<double[], double, bool> Terminator => terminator;

    public override object Clone() {
      return new Sphere(problemSize, min, max, seed);
    }
    private double evaluator(double[] x) {
      return (x[0] * x[0] + x[1] - 11) * (x[0] * x[0] + x[1] - 11) + (x[0] + x[1] * x[1] - 7) * (x[0] + x[1] * x[1] - 7);
    }

    private bool terminator(double[] x, double y) {
      return y == 0.0 && (
        (x[0] == 3.0 && x[1] == 2.0)
        || (x[0] == -2.805118 && x[1] == 3.131312)
        || (x[0] == -3.779310 && x[1] == 3.283186)
        || (x[0] == 3.584428 && x[1] == -1.848126)
      );
      // maxmimum at -0.270845, -0.923039
    }
  }


  public class StyblinskiTang : DoubleTypedProblem {

    public StyblinskiTang(int problemSize = 100, double min = -5.0, double max = 5.0, int seed = -1)
      : base(problemSize, min, max, seed) {
    }

    public override Func<double[], double> Evaluator => evaluator;

    public override Func<double[], double, bool> Terminator => terminator;

    public override object Clone() {
      return new Sphere(problemSize, min, max, seed);
    }
    private double evaluator(double[] x) {
      return x.Sum(i => x[0] * x[0] * x[0] * x[0] - 16.0 * x[0] * x[0] + 5.0 * x[0]) / 2.0;
    }

    private bool terminator(double[] x, double y) {
      return y == -39.16599*x.Length && x.All(i => i == -2.903534);
    }
  }
}

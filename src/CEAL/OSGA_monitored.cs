﻿using Ai.Hgb.Dat.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static Main.DataStructures;

namespace CEAL.Main {
  public class OSGAm {

    public ISocket Socket { get; set; }
    public string Group { get; set; }
    public string Name { get; set; }  

    public int PopSize { get; set; }
    public int Iterations { get; set; }
    public double MaxSelPres { get; set; } 
    public double MutationRate { get; set; }


    public OSGAm(string name, string group, ISocket socket) {
      Name = name;
      Group = group;
      Socket = socket;
      group = Guid.NewGuid().ToString();

      PopSize = 1000;
      Iterations = 1000;
      MaxSelPres = 1000;
      MutationRate = 0.1;
    }

    public void Run(int seed, int n) {
      double minX = -5.12;
      double maxX = 5.12;
      var rand = new Random(seed);

      var result = Run(rand, popSize: PopSize, iterations: Iterations, maxSelPres: MaxSelPres, mutationRate: MutationRate,
     obj: Rastrigin,
     // x ~(i.i.d) U(0,1) * 10.24 - 5.12
     creator: () => Enumerable.Range(1, n).Select(_ => rand.NextDouble() * (maxX - minX) + minX).ToArray(),
     // random parent selection
     selector: (f) => rand.Next(f.Length),
     // single point crossover
     crossover: (p0, p1, cand) =>
     {
       int cut = rand.Next(cand.Length);
       Array.Copy(p0, cand, cut);
       Array.Copy(p1, cut, cand, cut, cand.Length - cut);
     },
     // single point manipulation
     manipulator: (p) => p[rand.Next(n)] = rand.NextDouble() * (maxX - minX) + minX,
     output: true
  );

      double bestFit = result.Item2;
      Console.WriteLine("best fitness: {0}", bestFit);

    }

    private double Rastrigin(double[] x) {
      return 10.0 * x.Length + x.Sum(xi => xi * xi - 10.0 * Math.Cos(2.0 * Math.PI * xi));
    }

    private Tuple<T, double> Run<T>(Random rand, int popSize, int iterations, double maxSelPres, double mutationRate, Func<T, double> obj,
    Func<T> creator, Func<double[], int> selector, Action<T, T, T> crossover, Action<T> manipulator,
    bool output = false)
    where T : ICloneable {
      // generate random pop
      T[] pop = Enumerable.Range(0, popSize).Select(_ => creator()).ToArray();
      // evaluate initial pop
      double[] fit = pop.Select(p => obj(p)).ToArray();

      // arrays for next pop (clone current pop because solutions are reused)
      T[] popNew = pop.Select(pi => (T)pi.Clone()).ToArray();
      double[] fitNew = new double[popSize];

      var bestSolution = (T)pop.First().Clone(); // take a random solution (don't care)
      double bestFit = fit.First();

      // run generations
      double curSelPres = 0;
      for (int g = 0; g < iterations && curSelPres < maxSelPres; g++) {
        // keep the first element as elite
        int i = 1;
        int genEvals = 0;
        do {
          var p1Idx = selector(fit);
          var p2Idx = selector(fit);

          var p1 = pop[p1Idx];
          var p2 = pop[p2Idx];
          var f1 = fit[p1Idx];
          var f2 = fit[p2Idx];

          // generate candidate solution (reuse old solutions)
          crossover(p1, p2, popNew[i]);
          // optional mutation
          if (rand.NextDouble() < mutationRate) {
            manipulator(popNew[i]);
          }

          double f = obj(popNew[i]);
          genEvals++;
          // if child is better than best (strict offspring selection)
          if (f < Math.Min(f1, f2)) {
            // update best fitness
            if (f < bestFit) {
              bestFit = f;
              bestSolution = (T)popNew[i].Clone(); // overall best
            }

            // keep
            fitNew[i] = f;
            i++;
          }

          curSelPres = genEvals / (double)popSize;

        } while (i < popNew.Length && curSelPres < maxSelPres);

        Console.WriteLine("generation {0} obj {1:0.000} sel. pres. {2:###.0}", g, bestFit, curSelPres);

        // PUBLISH
        var dtnow = DateTime.Now;
        var dt = String.Format("{0:yyyy-MM-dd-hh-mm-ss-fff}", dtnow); // YYYY-MM-DD-HH-mm-ss-SSS
        var msg_fit = new DmonItem($"{Name}-fit", Group, 1, $"{Name}: fit", bestFit, dt, dt);
        var msg_sp = new DmonItem($"{Name}-sp", Group, 2, $"{Name}: spres", curSelPres, dt, dt);
        Socket.Publish<DmonItem>($"gae/run/{Name}/fit", msg_fit);
        Socket.Publish<DmonItem>($"gae/run/{Name}/sp", msg_sp);
        //Task.Delay(100).Wait();

        // swap 
        var tmpPop = pop;
        var tmpFit = fit;
        pop = popNew;
        fit = fitNew;
        popNew = tmpPop;
        fitNew = tmpFit;

        // keep elite
        popNew[0] = (T)bestSolution.Clone();
        fitNew[0] = bestFit;
      }

      return Tuple.Create(bestSolution, bestFit);
    }

    
  }
}

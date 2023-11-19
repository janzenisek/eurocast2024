using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEAL.Main {

  public interface IAlgorithm<T> : ICloneable where T : ICloneable {
    string Id { get; }

    // Problem
    IProblem<T> Problem { get; set; }

    Task<Tuple<T, double>> Run();
    Task Pause();
    Task Resume();
    Task Stop();
    void Cancel();
  }

  public interface IGeneticAlgorithm<T> : IAlgorithm<T> where T : ICloneable {

    // Hyperparameter
    int PopulationSize { get; set; }
    int Generations { get; set; }
    double MutationRate { get; set; }
  }

  public class PGA<T> : IGeneticAlgorithm<T> where T : ICloneable {
    public string Id { get => id; }

    // Problem
    public IProblem<T> Problem { get; set; }

    // Hyperparameters
    public int PopulationSize { get; set; }
    public int Generations { get; set; }
    public double MutationRate { get; set; }

    // Additional hyperparameters
    public int Elites { get; set; }
    public double MaximumSelectionPressure { get; set; }

    // MEM parameters
    public Func<T, Tuple<T, double>> LocalSearch { get; set; }
    public double LocalSearchRate { get; set; }

    // Island GA Parameters
    public Func<string, int, Tuple<T[], double[]>> Immigrate { get; set; }
    public double EpochTriggeringFailureRate { get; set; }
    public double ImmigrationRate { get; set; }
    public Func<string, T[], double[], CancellationToken, Task> Migrate { get; set; }



    private string id;
    private Random rnd;
    private int seed;
    private CancellationTokenSource cts;
    private Task<Tuple<T, double>> runner;

    public PGA(string id, int populationSize = 1000, int generations = 5000, double mutationRate = 0.1, int seed = -1) {
      this.id = id;
      rnd = seed < 0 ? new Random() : new Random(seed);
      this.seed = seed;
      this.cts = new CancellationTokenSource();

      PopulationSize = populationSize;
      Generations = generations;
      MutationRate = mutationRate;
      Elites = 1;
    }

    public PGA(string id, IProblem<T> problem, int populationSize = 1000, int generations = 5000, double mutationRate = 0.1, int elites = 1, int seed = -1)
      : this(id, populationSize, generations, mutationRate, seed) {
      Problem = problem;
    }

    public object Clone() {
      return new PGA<T>(this.id, seed);
    }

    public Task<Tuple<T, double>> Run() {
      if (runner == null) {
        var token = cts.Token;
        runner = Task.Run(() => {
          return GA(token);
          //return OSGA(token);
        }, token);
      }

      return runner;
    }

    private Tuple<T, double> GA(CancellationToken token) {
      // generate initial population
      T[] population = Enumerable.Range(0, PopulationSize).Select(_ => Problem.Creator()).ToArray();
      // evaluate initial population
      double[] fit = population.Select(p => Problem.Evaluator(p)).ToArray();
      
      // start optional migration service
      if(Migrate != null) {
        Migrate(id, population, fit, token);
      }

      // setup array for next population (clone to enable elitism in generation 0)
      T[] populationNew = population.Select(p => (T)p.Clone()).ToArray();
      double[] fitNew = new double[PopulationSize];

      // take a random solution as current best
      var bestSolution = (T)population.First().Clone();
      double bestFit = fit.First();

      double failureCount = 0;

      // run generations
      for (int g = 0; g < Generations && !Problem.Terminator(bestSolution, bestFit); g++) {
        if (token.IsCancellationRequested) break;

        // keep the first <property Elites> elements as elites
        int i = Elites;
        int candidates = 0;

        failureCount++;
        do {
          if (token.IsCancellationRequested) break;

          // select parents
          var p1Idx = Problem.Selector(fit);
          var p2Idx = Problem.Selector(fit);
          var p1 = population[p1Idx];
          var p2 = population[p2Idx];
          var f1 = fit[p1Idx];
          var f2 = fit[p2Idx];

          // generate candidate solution
          Problem.Crossover(p1, p2, populationNew[i]);
          // optional mutation
          if (rnd.NextDouble() < MutationRate) {
            Problem.Mutator(populationNew[i]);
          }

          // evaluate candidate solution
          double f = Problem.Evaluator(populationNew[i]);

          // optional local search
          if (LocalSearch != null && rnd.NextDouble() < LocalSearchRate) {
            var optimizedCandidate = LocalSearch(populationNew[i]);
            if (optimizedCandidate.Item2 < f) {
              // lamarck evolution:
              populationNew[i] = optimizedCandidate.Item1;
              // baldwin evolution:
              f = optimizedCandidate.Item2;
            }
          }          

          candidates++;          
          if (f < bestFit) {
            bestFit = f;
            bestSolution = (T)populationNew[i].Clone(); // overall best
            failureCount = 0;
          }
          fitNew[i] = f;
          i++;
          
        } while (i < populationNew.Length);
        Console.WriteLine("generation {0} obj {1}", g, bestFit);

        // optional immigration
        if(Immigrate != null && failureCount / Generations >= EpochTriggeringFailureRate) {
          var immigrants = Immigrate(id, (int) (PopulationSize * ImmigrationRate));
          for(int im = 0; im < immigrants.Item1.Length; im++) {
            var immigrationIdx = rnd.Next(Elites, populationNew.Length);
            populationNew[immigrationIdx] = immigrants.Item1[im];
            fitNew[immigrationIdx] = immigrants.Item2[im];
          }
        }

        // swap
        var tmpPopulation = population;
        var tmpFit = fit;
        population = populationNew;
        fit = fitNew;
        populationNew = tmpPopulation;
        fitNew = tmpFit;

        // keep elite
        populationNew[0] = (T)bestSolution.Clone();
        fitNew[0] = bestFit;
      }

      cts.Cancel(); // TODO: move or include
      return Tuple.Create(bestSolution, bestFit);
    }

    private Tuple<T, double> OSGA(CancellationToken token) {
      // generate initial population
      T[] population = Enumerable.Range(0, PopulationSize).Select(_ => Problem.Creator()).ToArray();
      // evaluate initial population
      double[] fit = population.Select(p => Problem.Evaluator(p)).ToArray();

      // setup array for next population (clone to enable elitism in generation 0)
      T[] populationNew = population.Select(p => (T)p.Clone()).ToArray();
      double[] fitNew = new double[PopulationSize];

      // take a random solution as current best
      var bestSolution = (T)population.First().Clone();
      double bestFit = fit.First();

      // run generations
      double currentSelectionPressure = 0.0;
      MaximumSelectionPressure = MaximumSelectionPressure > 0 ? MaximumSelectionPressure : double.MaxValue;
      for (int g = 0; g < Generations && currentSelectionPressure < MaximumSelectionPressure && !Problem.Terminator(bestSolution, bestFit); g++) {
        if (token.IsCancellationRequested) break;

        // keep the first <property Elites> elements as elites
        int i = Elites;
        int candidates = 0;

        do {
          if (token.IsCancellationRequested) break;

          // select parents
          var p1Idx = Problem.Selector(fit);
          var p2Idx = Problem.Selector(fit);
          var p1 = population[p1Idx];
          var p2 = population[p2Idx];
          var f1 = fit[p1Idx];
          var f2 = fit[p2Idx];

          // generate candidate solution
          Problem.Crossover(p1, p2, populationNew[i]);
          // optional mutation
          if (rnd.NextDouble() < MutationRate) {
            Problem.Mutator(populationNew[i]);
          }

          // evaluate candidate solution
          double f = Problem.Evaluator(populationNew[i]);

          // optional local search
          if (LocalSearch != null) {
            var optimizedCandidate = LocalSearch(populationNew[i]);
            if (optimizedCandidate.Item2 < f) {
              // lamarck evolution:
              populationNew[i] = optimizedCandidate.Item1;
              // baldwin evolution:
              f = optimizedCandidate.Item2;
            }
          }


          candidates++;
          // offspring selection
          if (f < Math.Min(f1, f2)) {
            if (f < bestFit) {
              bestFit = f;
              bestSolution = (T)populationNew[i].Clone(); // overall best
            }
            // keep offspring
            fitNew[i] = f;
            i++;
          }

          currentSelectionPressure = candidates / (double)PopulationSize;
        } while (i < populationNew.Length && currentSelectionPressure < MaximumSelectionPressure);
        Console.WriteLine("generation {0} obj {1} sel. pres. {2:###.0}", g, bestFit, currentSelectionPressure);

        // swap
        var tmpPopulation = population;
        var tmpFit = fit;
        population = populationNew;
        fit = fitNew;
        populationNew = tmpPopulation;
        fitNew = tmpFit;

        // keep elite
        populationNew[0] = (T)bestSolution.Clone();
        fitNew[0] = bestFit;
      }

      return Tuple.Create(bestSolution, bestFit);
    }

    public Task Pause() {
      cts.Cancel();
      return runner;
    }

    public Task Resume() {
      cts = new CancellationTokenSource();
      return Run();
    }

    public Task Stop() {
      cts.Cancel();
      return runner;
    }
    public void Cancel() {
      cts.Cancel();
    }
  }

  // TODO:
  // - extract problem type to problem
  // - generalize; currently: 1+1 ES
  public class PES : IAlgorithm<double[]> {
    public string Id { get => id; }

    public IProblem<double[]> Problem { get; set; }

    public double[] Candidate { get => candidate; set { candidate = value; } }    

    public int Generations { get; set; }


    private string id;
    private Random rnd;
    private int seed;
    private CancellationTokenSource cts;
    private Task<Tuple<double[], double>> runner;

    private double[] candidate;

    public PES(string id, IProblem<double[]> problem, int generations = 100, int seed = -1) {
      this.id = id;
      rnd = seed < 0 ? new Random() : new Random(seed);
      this.seed = seed;      
      this.cts = new CancellationTokenSource();
      
      Problem = problem;
      Generations = generations;      
    }

    public PES(string id, IProblem<double[]> problem, double[] candidate, int generations = 100, int seed = -1)
      : this(id, problem, generations, seed) {
      this.candidate = candidate;
    }

    public Task<Tuple<double[], double>> Run() {
      var token = cts.Token;
      runner = Task.Run(() => {
        //return OnePlusOneES_OneHotSelfAdaptiveMutation(this.candidate);
        return OnePlusOneES_IncrementalSelfAdaptiveMutation(this.candidate);
        //return OnePlusOneES_AdaptiveMutation(this.candidate);
      }, token);

      return runner;
    }

    public Func<double[], Tuple<double[], double>> Execute() {
      //return OnePlusOneES_OneHotSelfAdaptiveMutation;
      return OnePlusOneES_IncrementalSelfAdaptiveMutation;
      //return OnePlusOneES_AdaptiveMutation;
    }

    // "one hot" mutation
    private Tuple<double[], double> OnePlusOneES_OneHotSelfAdaptiveMutation(double[] candidate) {
      if (candidate == null) candidate = Problem.Creator();
      double fit = Problem.Evaluator(candidate);

      // initialize individual mutation rates (i.e. per gene inside the chromosome (i.e. solution candidate))
      double[] mutationRates = Enumerable.Range(0, candidate.Length).Select(x => 0.1).ToArray();

      for (int g = 0; g < Generations && fit != 0.0; g++) {
        var candidateNew = (double[])candidate.Clone();
        double fitNew = fit;

        for (int i = 0; i < candidateNew.Length; i++) {
          var candidateMutated = (double[])candidate.Clone();
          candidateMutated[i] *= mutationRates[i];
          var fitMutated = Problem.Evaluator(candidateMutated);

          if (fitMutated < fit) {
            candidateNew[i] = candidateMutated[i];
            mutationRates[i] *= 1.5;
          }
          else {
            mutationRates[i] *= Math.Pow(1.5, -0.25); // 1.5^-(1/4)
          }
        }

        // check if combined mutations are better than original candidate
        fitNew = Problem.Evaluator(candidateNew);
        if (fitNew < fit) {
          candidate = candidateNew;
          fit = fitNew;
        }
      }

      return Tuple.Create(candidate, fit);
    }
    
    private Tuple<double[], double> OnePlusOneES_IncrementalSelfAdaptiveMutation(double[] candidate) {
      if (candidate == null) candidate = Problem.Creator();
      double fit = Problem.Evaluator(candidate);

      // initialize individual mutation rates (i.e. per gene inside the chromosome (i.e. solution candidate))
      double[] mutationRates = Enumerable.Range(0, candidate.Length).Select(x => 0.1).ToArray();
      int[] executionOrder = Enumerable.Range(0, candidate.Length).ToArray();

      var candidateNew = (double[])candidate.Clone();
      double fitNew = fit;

      for (int g = 0; g < Generations && fit != 0.0; g++) {
        executionOrder = executionOrder.Shuffle(rnd).ToArray();                
        for(int i = 0; i < candidateNew.Length; i++) {
          var idx = executionOrder[i];

          var candidateMutated = (double[])candidateNew.Clone();
          candidateMutated[idx] *= mutationRates[idx];
          var fitMutated = Problem.Evaluator(candidateMutated);

          if(fitMutated < fitNew) {
            candidateNew[idx] = candidateMutated[idx];
            mutationRates[idx] *= 1.5;
            fitNew = fitMutated;
          } else {
            mutationRates[idx] *= Math.Pow(1.5, -0.25); // 1.5^-(1/4)
          }
        }
      }

      // sanity check (not necessary)
      fitNew = Problem.Evaluator(candidateNew);
      if (fitNew < fit) {
        candidate = candidateNew;
        fit = fitNew;
      }

      return Tuple.Create(candidate, fit);
    }

    private Tuple<double[], double> OnePlusOneES_AdaptiveMutation(double[] candidate) {
      if (candidate == null) candidate = Problem.Creator();
      double fit = Problem.Evaluator(candidate);

      // initialize individual mutation rates (i.e. per gene inside the chromosome (i.e. solution candidate))
      double[] mutationRates = Enumerable.Range(0, candidate.Length).Select(x => rnd.NextGaussian_BoxMuller(0.0, 1.0)).ToArray();      

      var candidateNew = (double[])candidate.Clone();
      double fitNew = fit;

      for (int g = 0; g < Generations && Problem.Terminator(candidateNew, fitNew); g++) {
        for (int i = 0; i < candidateNew.Length; i++) {
          candidateNew[i] *= mutationRates[i];
        }
        fitNew = Problem.Evaluator(candidateNew);
        if(fitNew < fit) {
          candidate = candidateNew;
          fit = fitNew;
          for (int r = 0; r < mutationRates.Length; r++) mutationRates[r] *= 1.5;
        } else {
          for (int r = 0; r < mutationRates.Length; r++) mutationRates[r] = rnd.NextGaussian_BoxMuller(0.0, 1.0);// Math.Pow(1.5, -0.25);
        }       
      }

      return Tuple.Create(candidate, fit);
    }

    public void Cancel() {
      cts.Cancel();
    }

    public object Clone() {
      throw new NotImplementedException();
    }

    public Task Pause() {
      cts.Cancel();
      return runner;
    }

    public Task Resume() {
      throw new NotImplementedException();
    }

    public Task Stop() {
      cts.Cancel();
      return runner;
    }
  }

  public class GAServices<T> {
    
    // private ISocket client;

    public GAServices(string host) {
      // client = new SocketFactory(host);
    }

    public Tuple<T[], double[]> Immigrate(string id, int count) {
      // return client.Request<Tuple<T, double[]>>($"{id}/migration", new Message(count));
      
      // mock response
      var x = (T[])Array.CreateInstance(typeof(T), count);
      for (int i = 0; i < x.Length; i++) x[i] = (T)Activator.CreateInstance(typeof(T), new object[] { 2 });
      return Tuple.Create(x , new double[count]);
    }

    public Task Migrate(string id, T[] migrants, double[] migrantFits, CancellationToken token) {
      // mock server
      return Task.Run(() =>
      {
        // client.Subscribe($"{id}/migration", () => {
        // 
        // });
      }, token);
    }
  }
  
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using log4net.Config;
using SharpNeat.Core;
using SharpNeat.EvolutionAlgorithms;
using SharpNeat.Genomes.Neat;
using SharpNeat.Phenomes;

namespace UltimateTicTacToe
{
    public class Board
    {
        /// <summary>
        /// Data
        ///     0  1  2
        ///     3  4  5
        ///     6  7  8
        /// </summary>

        // Columns
        // 0000 0000 0100 1001 = 73
        // 0000 0000 1001 0010 = 146
        // 0000 0001 0010 0100 = 292

        // Rows
        // 0000 0000 0000 0111 = 7
        // 0000 0000 0011 1000 = 56
        // 0000 0001 1100 0000 = 448

        // Cross
        // 0000 0001 0001 0001 = 273
        // 0000 0000 0101 0100 = 84
        private static ushort[] _winningCondition = new ushort[] {73, 146, 292, 7, 56, 448, 273, 84};
        private ushort _movesLeft = 9;
        private bool _IsXturn = false; 
        
        // X - Player
        private ushort _xPos = 0;

        // O - Player
        private ushort _oPos = 0;
        
        public void MakeMove(ushort position)
        {
            if (_IsXturn)
            {
                // Set a bit at position to 1.
                _xPos |= (ushort)(1 << position);
            }
            else
            {
                // Set a bit at position to 1.
                _oPos |= (ushort)(1 << position);
            }

            _movesLeft -= 1;
        }

        public bool IsDraw()
        {
            return _movesLeft == 0;
        }

        public bool IsValid(ushort position)
        {
            // Return whether bit at position is set to 0.
            return ((_xPos | _oPos) & (1 << position)) == 0;
        }
        
        public short GetSquareValue(ushort position, bool isPlayerX)
        {
            if ((_xPos & (1 << position)) == 1)
            {
                return (short) (isPlayerX ?  1 : -1);
            }
            
            if ((_oPos & (1 << position)) == 1)
            {
                return (short) (isPlayerX ?  -1 : 1);
            }

            // Return whether bit at position is set to 0.
            return 0;
        }

        public bool HasXWon()
        {
            return _movesLeft <= 6 && _winningCondition.Any(condition => (_xPos & condition) == condition);
        }

        public bool HasOWon()
        {
            return _movesLeft <= 6 && _winningCondition.Any(condition => (_oPos & condition) == condition);
        }

        /// <summary>
        /// Returns the player that won the game,
        /// 0 = draw,
        /// 1 = playerX,
        /// 2 = playerO
        /// 
        /// </summary>
        /// <param name="playerX"></param>
        /// <param name="playerO"></param>
        /// <returns></returns>
        public static ushort PlayUntilWin(IPlayer playerX, IPlayer playerO)
        {
            
            Board board = new Board();
            while (true)
            {
                ushort move;
                move = board._IsXturn ? playerX.GetMove(board) : playerO.GetMove(board);

                // If the move was not valid
                if (!board.IsValid(move))
                {
                    return board._IsXturn ? (ushort) 2 : (ushort) 1;
                }

                if (board.HasOWon())
                {
                    return 2;
                }

                if (board.HasXWon())
                {
                    return 1;
                }
                
                if (board.IsDraw())
                {
                    return 0;
                }
                
                board._IsXturn = !board._IsXturn;
            }
        }
    }
    
    public interface IPlayer
    {
        public ushort GetMove(Board board);
    }

    public class NeatPlayer : IPlayer
    {
        private readonly IBlackBox _brain;
        private readonly bool _isPlayerX;
        public NeatPlayer(IBlackBox brain, bool isPlayerX=true)
        {
            _brain = brain;
            _isPlayerX = isPlayerX;
        }
        
        public ushort GetMove(Board board)
        {
            // Clear the network
            _brain.ResetState();

            // Convert the game board into an input array for the network
            setInputSignalArray(_brain.InputSignalArray, board);

            // Activate the network
            _brain.Activate();
            
            ushort bestMove = 9;
            var max = Double.MinValue;
            for (ushort pos = 0; pos < 9; pos++)
            {
                if (!board.IsValid(pos))
                {
                    continue;
                }

                var score = _brain.OutputSignalArray[pos];

                // This is the first move
                if (bestMove != 9 && !(max < score)) continue;
                bestMove = pos;
                max = score;
            }

            // Return the best position
            return bestMove;
        }
        
        
        // Loads the board into the input signal array.
        private void setInputSignalArray(ISignalArray inputArr, Board board)
        {
            for (ushort pos = 0; pos < 9; pos++)
            {
                inputArr[pos] = board.GetSquareValue(pos, _isPlayerX);
            }
        }
    }
    
    public class RandomPlayer : IPlayer
    {
        private Random _random = new Random();
        
        public ushort GetMove(Board board)
        {
            int count = _random.Next(0, 9); 
            while (true)
            {
                ushort move = (ushort) (count % 9);

                if (board.IsValid(move))
                {
                    return move;
                }

                count++;
            }
        }
    }

    class Evaluator : IPhenomeEvaluator<IBlackBox>
    {
        bool shouldEnd = false;
        private ulong _evalCount = 0;
        public FitnessInfo Evaluate(IBlackBox phenome)
        {
            double fitness = 0;
            ushort result = 0;
            RandomPlayer randomPlayer = new RandomPlayer();
            NeatPlayer neatPlayer = new NeatPlayer(phenome);
                     
                     
            // Play 50 games as X against a random player
            for (int i = 0; i < 1; i++)
            {
                // Compete the two players against each other.
                result = Board.PlayUntilWin(neatPlayer, randomPlayer);
         
                // Update the fitness score of the network
                if (result == 1) fitness += 10;
            }
         
            neatPlayer = new NeatPlayer(phenome, false);
            
            // Play 50 games as O against a random player
            for (int i = 0; i < 1; i++)
            {
                // Compete the two players against each other.
                result = Board.PlayUntilWin(randomPlayer, neatPlayer);
         
                // Update the fitness score of the network
                if (result == 2) fitness += 10;
            }
            
            // Update the evaluation counter.
            _evalCount++;
         
            // If the network plays perfectly, it will beat the random player
            // and draw the optimal player.
            if (fitness >= 100)
                shouldEnd = true;
         
            // Return the fitness score
            Console.WriteLine($"Evaluate {fitness}");
            return new FitnessInfo(fitness, fitness);
        }

        public void Reset() {}
        public ulong EvaluationCount => _evalCount;
        public bool StopConditionSatisfied => shouldEnd;
    }

    class TicTacToeExperiment: SimpleNeatExperiment
    {
        private static IPhenomeEvaluator<IBlackBox> _phenomeEvaluator = new Evaluator();
        
        public override IPhenomeEvaluator<IBlackBox> PhenomeEvaluator => _phenomeEvaluator;
        public override int InputCount => 9;

        public override int OutputCount => 9;

        public override bool EvaluateParents { get; } = true;
    }
    
    class Program
    {
        static NeatEvolutionAlgorithm<NeatGenome> _ea;
        const string CHAMPION_FILE = "tictactoe_champion.xml";
 
        static void Main(string[] args)
        {
            Console.WriteLine("Starting training");
            // Initialise log4net (log to console).
            // XmlConfigurator.Configure(new FileInfo("log4net.properties"));
 
            // Experiment classes encapsulate much of the nuts 
            // and bolts of setting up a NEAT search.
            TicTacToeExperiment experiment = new TicTacToeExperiment();
 
            // Load config XML.
            experiment.Initialize("TicTacToe");
 
            // Create evolution algorithm and attach update event.
            _ea = experiment.CreateEvolutionAlgorithm();
            _ea.UpdateEvent += new EventHandler(ea_UpdateEvent);
             
            // Start algorithm (it will run on a background thread).
            _ea.StartContinue();
 
            // Hit return to quit.
            Console.ReadLine();
        }
 
        static void ea_UpdateEvent(object sender, EventArgs e)
        {
            Console.WriteLine(string.Format("gen={0:N0} bestFitness={1:N6}",
                _ea.CurrentGeneration, _ea.Statistics._maxFitness));
             
            // Save the best genome to file
            var doc = NeatGenomeXmlIO.SaveComplete(
                new List<NeatGenome>() {_ea.CurrentChampGenome}, 
                false);
            doc.Save(CHAMPION_FILE);
        }
    }
    
    
}
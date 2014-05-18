using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LSHashing
{
    public partial class Form1 : Form
    {
        public Form1 ()
        {
            InitializeComponent();
        }

        #region Variables

        Thread nthread;

        /*stop watches to calculate running time*/
        Stopwatch sw = new Stopwatch(); 
        Stopwatch sw1 = new Stopwatch();


        List<string[]> sentences = new List<string[]>(); //sentences list
        List<string> candPairs = new List<string>();    //candidate pair list
        List<string> canPairsTreashold = new List<string>();    //candidate pair list after treshold calculation
        List<string> candPairsDist = new List<string>();    //candidate pair list after edit distance calculated       
        List<string> duplicates = new List<string>();   //list to hold duplicates

        Dictionary<int, int> hashShingledDocuments = new Dictionary<int, int>();    //Dictionry to hold shingles of all sentences
        Dictionary<int, List<int>>[] Dbuckets;  //2D dictionary array to hold buckets



        int[][] hashedShingledSentence;  //2D array to hold shingles of respective sentences
        int[][] sigMatrix;              //signature matrix
        int[] randomnumberhash;         
        int[] randomnumberhash2;
        
        /*otehr variabales*/
        int rows = 4;
        int bands = 6;
        int trows = 24;
        double treashold=0.5; //default treshold


        string path = "input.txt";
        bool started = false;
        #endregion



        #region Methods
        /*read the input file and put sentences to a list with the ID of sentence */
        private void readFile ()
        {
            string pline;

            try
            {
                StreamReader pfile = new StreamReader(path);
                int sentencCount = 1;
                while((pline = pfile.ReadLine()) != null)
                {

                    sentences.Add(new string[2]);

                   sentences[sentencCount - 1][0] = pline;
                   sentences[sentencCount - 1][1] = "S" + sentencCount.ToString();

                    sentencCount++;
                }
                pfile.Close();
            }
            catch(IOException ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }


        /*read the input file with prefix and put sentences to a list with the ID of sentence */
        private void readFilePreFix ()
        {
            string pline;

            try
            {
                StreamReader pfile = new StreamReader(path);
                int sentencCount = 1;
                while((pline = pfile.ReadLine()) != null)
                {

                    pline = pline.ToLower();
                    string[] temline = pline.Split(':');
                    sentences.Add(new string[2]);
                    temline[1] = temline[1].Remove(0, 1);

                    

                    sentences[sentencCount - 1][0] = temline[1];
                    sentences[sentencCount - 1][1] = temline[0];
                   
                    sentencCount++;
                }
                pfile.Close();
            }
            catch(IOException ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }


        /*divide the sentences to 3-Shingles and Add them to a Dictionary as well as to an array*/
        private void shingleByWord ()
        {
            hashedShingledSentence = new int[sentences.Count][];
            int count = 1;
            for(int i = 0; i < sentences.Count; i++)
            {
                
                
                string[] words = sentences[i][0].Split(' ');

                hashedShingledSentence[i] = new int[words.Length - 2];
                
                for(int j = 0; j < words.Length - 2; j++)
                {

                    string shingle = words[j] + words[j + 1] + words[j + 2]; ;
                    int temphash = shingle.GetHashCode();
                    if(!hashShingledDocuments.ContainsKey(temphash))
                    {
                        hashShingledDocuments.Add(temphash, temphash);
                        count++;
                    }
                    
                    hashedShingledSentence[i][j] = temphash;

                }
            }

        }

        /*Random number function for the sighash method*/
        private void randomNumforHash ()
        {
            Random ranNum = new Random();
            randomnumberhash = new int[trows];
            randomnumberhash2 = new int[trows];
            for(int i = 0; i < randomnumberhash.Length; i++)
            {
                randomnumberhash[i] = ranNum.Next(10000);
                randomnumberhash2[i] = ranNum.Next(10000);
            }

        }


        /*Hashing method for making the signature matrix*/
        private int sigHash (int i,int r)
        {
            unchecked
            {
                const int p = 16777619;
                int hash = (int)2166136261;


                hash = hash * (randomnumberhash[i] * (r+1) * p);

                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                if(hash>=Int32.MaxValue || hash<=Int32.MinValue)
                {
                    Console.WriteLine(hash);
                    hash = hash % 10001;
                }

                return hash;
            }
        
        }


        /*method for genarating the signature matrix*/
        private void genSigMatrix ()
        {
            randomNumforHash();
            sigMatrix = new int[trows][];

            for(int i = 0; i < trows; i++)
            {
                sigMatrix[i] = new int[sentences.Count];

                for(int j = 0; j < sentences.Count; j++)
                {
                    sigMatrix[i][j] = Int32.MaxValue;
                }
            }


            for(int i = 0; i < hashedShingledSentence.Length; i++)
            {
                for(int j = 0; j <  hashedShingledSentence[i].Length;j++ )
                {

                    if(hashShingledDocuments.ContainsKey(hashedShingledSentence[i][j]))
                        {
                            for(int m = 0; m < trows; m++)
                            {
                                sigMatrix[m][i] = Math.Min(sigMatrix[m][i], sigHash(m, hashedShingledSentence[i][j]));

                            }
                        }
                
                }
            
            }


        
        }


        /*method for looping through the signature matrix ,dividing to bands and hashing each column to buckets*/
        private void BandHashing ()
        {
           
            Dbuckets = new Dictionary<int, List<int>>[bands];

            for(int i = 0; i < bands; i++)
            {
              
                Dbuckets[i]= new Dictionary<int, List<int>>();

            }

            int[] tempcolumn = new int[rows];
            int count = 0;


            for(int i = 0; i < trows;i=i+rows)
            {
                for(int j = 0; j < sentences.Count;j++ )
                { 
                    for(int k = i; k <i+ rows;k++ )
                    {
                        tempcolumn[k-i]=sigMatrix[k][j];
                    }

                    int shash = bandColumnHash(tempcolumn);

                    if(Dbuckets[count].ContainsKey(shash))
                    {
                        Dbuckets[count][shash].Add(j);
                        
                      
                    }
                    else
                    {

                        Dbuckets[count].Add(shash, new List<int>());

                        Dbuckets[count][shash].Add(j);
                    }

                }

                count++;
            }
        
        }

        /*hash function for hashing of columns in bandhashing method*/
        private int bandColumnHash (int[] data)
        {
            unchecked
            {
                const int p = 16777619;
                int hash = (int)2166136261;

                for(int i = 0; i < data.Length; i++)
                    hash = (hash ^ data[i]) * p;

                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
               
                return hash;
            }
        }



        /*sort shingle hashes */
        private void sort ()
        {
            

            var items = from pair in hashShingledDocuments
		    orderby pair.Value ascending
		    select pair;

            hashShingledDocuments = items.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
            
            for(int i = 0; i < hashedShingledSentence.Length;i++ )
            {
                Array.Sort(hashedShingledSentence[i]);
            }


        }


        /*method for printing the output to GUI and to outputing to a file */
        private void output ()
        {
            
            try
            {
                using(System.IO.StreamWriter file = new System.IO.StreamWriter("scs2009_sentences.out"))
                {
                    foreach(string line in candPairsDist)
                    {
                       
                        SetText1(line + "\n");
                        file.WriteLine(line);

                    }
                }
            }
            catch(IOException ex)
            {
                MessageBox.Show(ex.ToString());
            }

            

        }

        


        /*method for making identifing candidate pairs in buckets*/
        private void ncandidatePairs ()
        {
            for(int i = 0; i < Dbuckets.Length; i++)
            {
                Dictionary<int, List<int>>.KeyCollection keyColl = Dbuckets[i].Keys;

                foreach(int d in keyColl)
                {
                    if(Dbuckets[i][d].Count > 1)
                    {
                        for(int k = 0; k < Dbuckets[i][d].Count; k++)
                        {
                            for(int m = 0; m < Dbuckets[i][d].Count; m++)
                            {
                                if(k != m)
                                {
                                    if(k < m)
                                    {

                                        candPairs.Add("S" + (Dbuckets[i][d][k] + 1) + " " + "S" + (Dbuckets[i][d][m] + 1));

                                    }

                                }
                            }

                        }

                    }

                }


            }
        }



        /*method for calculating edit distance */
        private int calcEditDist (int s1, int s2)
        {
            string[] a1;
            string[] a2;

            if(sentences[s1][0].Length > sentences[s2][0].Length)
            {
                a1 = sentences[s1][0].Split(' ');
                a2 = sentences[s2][0].Split(' ');
            }

            else
            {

                a2 = sentences[s1][0].Split(' ');
                a1 = sentences[s2][0].Split(' ');
            }

            string[] calctemp1 = a1.ToArray();
            string[] calctemp2 = a2.ToArray();


            string ctemp1 = string.Join("", calctemp1);
            string ctemp2 = string.Join("", calctemp2);



            if(ctemp1.Equals(ctemp2))
            {
                
                return 3;
            }

            int editDist = Math.Abs(a1.Length - a2.Length);



            if(editDist <= 1)
            {
                if(editDist == 1)
                {

                    for(int i = 0; i < a1.Length; i++)
                    {
                        string[] temp = a1.ToArray();
                        temp[i] = "";
                        string temp1 = string.Join("", temp);
                        string temp2 = string.Join("", a2);


                        if(temp1.Equals(temp2))
                        {

                            return 1;

                        }

                    }

                }
                if(editDist == 0)
                {
                    for(int i = 0; i < a1.Length; i++)
                    {
                        string[] tempa1 = a1.ToArray();
                        tempa1[i] = "";

                        for(int j = 0; j < a2.Length; j++)
                        {

                            string[] tempa2 = a2.ToArray();
                            tempa2[j] = "";
                            string temp1 = string.Join("", tempa1);
                            string temp2 = string.Join("", tempa2);

                            if(temp1.Equals(temp2))
                            {

                                return 1;

                            }
                        }
                    }

                }


            }
            else if(editDist == 0)
            {
                return 0;
            }
            else
            {
                return editDist;
            }


            return editDist + 5;
        }



        /*method for feeding the candidate pairs list to edit distance calculation*/
        private void feedCalcDist ()
        {
            for(int i = 0; i < canPairsTreashold.Count; i++)
            {
                string[] gg = canPairsTreashold[i].Split(' ');

                int a1 = Convert.ToInt32(gg[0].Remove(0, 1));
                int a2 = Convert.ToInt32(gg[1].Remove(0, 1));
                if(calcEditDist(a1 - 1, a2 - 1) <= 1)
                {
                    candPairsDist.Add( a1 + " " + a2);
                }

            }
            candPairsDist.Sort();
        }




        /*method to calculate treashold*/
        private void tresholdSimlarity ()
        {
            candPairs = candPairs.Distinct().ToList();

            for(int i = 0; i < candPairs.Count; i++)
            {


                string[] gg = candPairs[i].Split(' ');

                int a1 = Convert.ToInt32(gg[0].Remove(0, 1));
                int a2 = Convert.ToInt32(gg[1].Remove(0, 1));


                int[] temp1 = hashedShingledSentence[a1 - 1].ToArray();
                int[] temp2 = hashedShingledSentence[a2 - 1].ToArray();


                List<int> union = temp1.ToArray().Union(temp2.ToArray()).ToList();
                List<int> intersect = temp1.ToArray().Intersect(temp2.ToArray()).ToList();

                int intercount = intersect.Count;
                int unioncount = union.Count;
                if(((double)intercount / (double)unioncount) >= treashold)
                {

                    canPairsTreashold.Add(candPairs[i]);
                }

            }

        }
       

      

        private void button2_Click (object sender, EventArgs e)
        {

        }

        /*initiate calculation*/
        private void start ()
        {

            bands = Convert.ToInt32(textBox3.Text);
            rows = Convert.ToInt32(textBox4.Text);
            trows = bands * rows;
            treashold = Convert.ToDouble(textBox5.Text);

            sw1.Start();
            sw.Start();
            if(checkBox1.Checked == true)
            {
                readFilePreFix();
                SetText2("File Reading done -------- " + sw.ElapsedMilliseconds.ToString() + " ms\n");
                sw.Reset();
            }
            else
            {
                readFile();
                SetText2("File Reading done -------- " + sw.ElapsedMilliseconds.ToString() + " ms\n");
                sw.Reset();
            
            }
            sw.Start();
            shingleByWord();
            SetText2("Shingling done -------- " + sw.ElapsedMilliseconds.ToString() + " ms\n");
            sw.Reset();

            sw.Start();
            sort();
            SetText2("Sorting of Shingles done -------- " + sw.ElapsedMilliseconds.ToString() + " ms\n");
            sw.Reset();
           
            sw.Start();
            genSigMatrix();
            SetText2("Signature Matrix creation done -------- " + sw.ElapsedMilliseconds.ToString() + " ms\n");
            sw.Reset();
            
            sw.Start();
            BandHashing();
            SetText2("Hashing bands done -------- " + sw.ElapsedMilliseconds.ToString() + " ms\n");
            sw.Reset();
           
            sw.Start();
            ncandidatePairs();
            SetText2("Candidate pairing done -------- " + sw.ElapsedMilliseconds.ToString() + " ms\n");
            sw.Reset();
           
            sw.Start();
            tresholdSimlarity();
            SetText2("Treashold calculation done -------- " + sw.ElapsedMilliseconds.ToString() + " ms\n");
            sw.Reset();
            
            sw.Start();
            feedCalcDist();
            SetText2("Edit distance calculation done -------- " + sw.ElapsedMilliseconds.ToString() + " ms\n");
            sw.Reset();
            
            sw.Stop();
            sw1.Stop();
            SetText2("Total time Elapsed -------- "+sw1.ElapsedMilliseconds.ToString() + " ms\n");
            output();
            SetText2("Total Matches  "+candPairsDist.Count.ToString() + "\n");
            candPairs = candPairs.Distinct().ToList();
            
           
        }


        #endregion




        #region GUI related Code

        private void button1_Click (object sender, EventArgs e)
        {
            if(started == false)
            {
                nthread = new Thread(start);
                nthread.Start();
                started = true;
            }
            else
            {
                MessageBox.Show("Reset to run again!");
            }

            
            
        }

        private void button3_Click (object sender, EventArgs e)
        {
            if(nthread.IsAlive)
            {
                nthread.Abort();
                Form1 n = new Form1();
                this.Hide();
                n.Show();
            }
            else
            {
                Form1 n = new Form1();
                this.Hide();
                n.Show();
            
            }
        }


        delegate void SetTextCallback (string text);


        private void SetText2 (string text)
        {

            if(this.textBox2.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetText2);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                this.textBox2.AppendText(text);

            }
        }

        private void SetText1 (string text)
        {

            if(this.textBox1.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetText1);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                this.textBox1.AppendText(text);

            }
        }

       



        #endregion

        private void groupBox1_Enter (object sender, EventArgs e)
        {

        }

        private void Form1_FormClosed (object sender, FormClosedEventArgs e)
        {
            Application.Exit();
        }

        private void button4_Click (object sender, EventArgs e)
        {
            DialogResult file = openFileDialog1.ShowDialog();
            if(DialogResult.OK == file)
            {
                path = openFileDialog1.FileName;
                textBox6.Text = openFileDialog1.FileName;
            }
        }

    }
}

using System;
using System.IO;
using System.Windows.Forms;

namespace DLLRewriter
{
    class Program
    {
        private static bool MatchingSequence(byte[] needle, byte[] haystack, int iOffsetOfFirstMatchingByte)
        {
            if (iOffsetOfFirstMatchingByte + needle.Length >= haystack.Length) return false;
            for (int iX = 1; iX < needle.Length; iX++)
            {
                if (haystack[iOffsetOfFirstMatchingByte + iX] != needle[iX]) return false;
            }
            return true;
        }

        /// <summary>
        /// Given a base folder, find the latest version w.x.y.z subfolder
        /// </summary>
        static string GetLatestVersionFolder(string sBasePath)
        {
            try {
                if (!Directory.Exists(sBasePath))
                {
                    return null;
                }
                string[] sCandidates = Directory.GetDirectories(sBasePath, ("??.*.*.*"));
                if (sCandidates.Length < 1) return null;
                Array.Sort(sCandidates, (x, y) =>
                    {
                        try {
                            Version vx = new Version(Path.GetFileName(x));
                            Version vy = new Version(Path.GetFileName(y));
                            return -vx.CompareTo(vy);
                        }
                        catch
                        {
                            return -x.CompareTo(y);
                        }
                    }
                );
                return sCandidates[0];
            }
            catch
            {
                return null;
            }
        }

        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                string sFilename = String.Empty;
                if (args.Length < 1)
                {
                    OpenFileDialog ofd = new OpenFileDialog();
                    string sDefaultPath = null;

                    try {
                        string[] sCandidates =
                        {
                            String.Join(Path.DirectorySeparatorChar.ToString(), new[] { Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome SxS", "Application" }),
                            String.Join(Path.DirectorySeparatorChar.ToString(), new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application" }),
                        };
                        foreach (string sCandidate in sCandidates)
                        {
                            var sPath = GetLatestVersionFolder(sCandidate);
                            if (!String.IsNullOrEmpty(sPath))
                            {
                                if (sDefaultPath == null)
                                {
                                    sDefaultPath = sPath;
                                }

                                ofd.CustomPlaces.Add(sPath);
                            }
                        }
                    }
                    catch { /*non-critical*/ }


                    ofd.FileName = "chrome.dll";
                    if (!String.IsNullOrEmpty(sDefaultPath)) ofd.InitialDirectory = sDefaultPath;
                    if (DialogResult.OK == ofd.ShowDialog())
                    {
                        sFilename = ofd.FileName;
                        // TODO: If filename isn't chrome.dll, should we change it?
                    }
                    else
                    {
                        sFilename = null;
                    }
                    ofd.Dispose();
                    if (String.IsNullOrEmpty(sFilename))
                    {
                        Console.WriteLine("Usage:\n\n\tDLLRewriter.exe <path-to-chrome.dll>");
                        return 1;
                    }
                }
                else
                {
                    sFilename = args[0];
                }

             //   string sFilename = args[0];
                     //  @"C:\users\ericlaw\appdata\local\google\chrome SxS\application\51.0.2704.0\chrome.dll";
            //         @"C:\Program Files (x86)\Google\Chrome\Application\51.0.2687.0\chrome.dll";
                byte[] arrCmdId = { 0x13, 0xba, 0xc3, 0x88, 0, 0, 0x41, 0xb8 };  // IDC_ROUTE_MEDIA 35011 0x88c3.... Bytes before and after appear to be fairly common, but not sure why yet...

                // This value isn't stable because generated_resources.h that contains the string ID changes from version to version.
                // byte[] arrStrId = { 0x24, 0x39, 0, 0 };  // IDS_MEDIA_ROUTER_MENU_ITEM_TITLE 14628 0x3924

                // Consider replacing with IDS_TAB_CXMENU_CLOSETAB  0x2b 0x42 to reuse the existing "Close tab" menu command

                //int ixLastCmdId = 0;
                //int ixLastStrId = 0;
                //const int MAX_DIFF = 16;

                Console.WriteLine("Loading {0}", sFilename);
                byte[] arrFile = File.ReadAllBytes(sFilename);
                Console.WriteLine("{0} bytes read. Searching for Cast command (IDC_ROUTE_MEDIA)...", arrFile.Length.ToString("N0"));

                bool bFound = false;
                bool bBackedUpOriginal = false;
                for (int iX = 0; iX < arrFile.Length - arrCmdId.Length; iX++)
                {
                    if (arrFile[iX] == arrCmdId[0])
                    {
                        if (MatchingSequence(arrCmdId, arrFile, iX))
                        {
                            /*ixLastCmdId = iX;
                            string sMark = "";
                            //if (ixLastStrId + MAX_DIFF > ixLastCmdId)
                            {
                             //   sMark = ("<<<< delta=" + (ixLastCmdId - ixLastStrId).ToString());
                            }
                            else
                            {
                                //   continue;
                            }*/

                            if (!bFound)
                            {
                                var sOriginalFilename = sFilename + ".orig";
                                try
                                {
                                    File.WriteAllBytes(sOriginalFilename, arrFile);
                                    bBackedUpOriginal = true;
                                }
                                catch { }
                            }


                            Console.WriteLine("0x{0:x} cmdId {1}", iX, "\nChanging 0xC3 0x88 to 0xDF 0x84 will change CAST to CLOSETAB");

                            arrFile[iX + 2] = 0xDF;
                            arrFile[iX + 3] = 0x84;
                            bFound = true;
                        }
                        continue;
                    }

                    /*
                    // On Canary 51, the string ID is 6 bytes after the CmdID
                    if (arrFile[iX] == arrStrId[0])
                    {
                        if (MatchingSequence(arrStrId, arrFile, iX))
                        {
                            ixLastStrId = iX;
                            string sMark = "";
                            if (ixLastCmdId + MAX_DIFF > ixLastStrId)
                            {
                                sMark = ("<<<< delta=" + (ixLastStrId - ixLastCmdId).ToString());
                            }
                            else
                            {
                               // continue;
                            }
                            Console.WriteLine("0x{0:x} strID {1}", ixLastStrId, sMark);
                        }
                    continue;
                    }*/
                }

                if (bFound)
                {
                    if (!bBackedUpOriginal)
                    {
                        string sNewFilename = sFilename + ".updated";
                        try
                        {
                            File.WriteAllBytes(sNewFilename, arrFile);
                            Console.WriteLine("Wrote modified " + sNewFilename);
                        }
                        catch (Exception eeX)
                        {
                            Console.WriteLine("Failed to write modified file due to " + eeX.Message + "\nRun elevated, if you haven't already.");
                        }
                    }
                    else
                    {
                        RewriteFile(sFilename, arrFile);
                    }
                }
                else
                {
                    Console.WriteLine("Unable to find Command Identifier.");
                }
                Console.WriteLine("Press any key...");
                Console.ReadKey();
                return 0;
            }
            catch (Exception eX)
            {
                Console.WriteLine(eX.Message + "\n" + eX.StackTrace);
                return 2;
            }
        }

        private static void RewriteFile(string sFilename, byte[] arrFile)
        {
            do
            {
                try
                {
                    File.WriteAllBytes(sFilename, arrFile);
                    Console.WriteLine("Overwrote original with modified " + sFilename);
                    return;
                }
                catch (Exception eeX)
                {
                    Console.WriteLine("Failed to write modified file due to " + eeX.Message + "\nClose Chrome or run elevated, if you haven't already.");
                }

                Console.Write("Retry? [R/N]");
            }
            while (Char.ToLower(Console.ReadKey().KeyChar) != 'n');
        }
    }
}

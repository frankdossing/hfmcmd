using System;
using System.IO;

using log4net;

// We have to include the following lib even when using dynamic, since it contains
// the definition of the enums
using HSVJOURNALLOADACVLib;

using Command;
using HFMCmd;
using Utilities;


namespace HFM
{

    public class JournalLoad
    {

        /// <summary>
        /// Collection class holding options that can be specified when loading
        /// journals.
        /// </summary>
        [Setting("Delimiter", "Journal file delimiter",
                 ParameterType = typeof(string))] // TODO: Validation list
        public class LoadOptions : LoadExtractOptions
        {
            [Factory]
            public LoadOptions(JournalLoad jl) :
                base(typeof(IHsvLoadExtractOptions), typeof(IHsvLoadExtractOption),
                     typeof(HSV_JOURNALLOAD_OPTION),
                     (IHsvLoadExtractOptions)jl.HsvJournalLoadACV.LoadOptions)
            {
            }
        }


        /// <summary>
        /// Collection class holding options that can be specified when extracting
        /// journals.
        /// </summary>
        [Setting("Delimiter", "File delimiter used in journal extract file",
                 ParameterType = typeof(string)), // TODO: Validation list
         Setting("Regular", "True to extract regular journals"),
         Setting("Standard", "True to extract standard journal templates"),
         Setting("Recurring", "True to extract recurring journal templates")]
        public class ExtractOptions : LoadExtractOptions
        {
            [Factory]
            public ExtractOptions(JournalLoad jl) :
                base(typeof(IHsvLoadExtractOptions), typeof(IHsvLoadExtractOption),
                     typeof(HSV_JOURNALDATAEXTRACT_OPTION),
                     (IHsvLoadExtractOptions)jl.HsvJournalLoadACV.ExtractOptions)
            {
            }
        }


        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Reference to HFM HsvJournalLoadACV object
#if LATE_BIND
        internal readonly dynamic HsvJournalLoadACV;
#else
        internal readonly HsvJournalLoadACV HsvJournalLoadACV;
#endif


        [Factory]
        public JournalLoad(Session session)
        {
            _log.Trace("Constructing JournalLoad object");
#if LATE_BIND
            HsvJournalLoadACV = HFM.CreateObject("Hyperion.HsvJournalLoadAVC");
#else
            HsvJournalLoadACV = new HsvJournalLoadACV();
#endif
            HsvJournalLoadACV.SetSession(session.HsvSession);
        }


        [Command("Loads jounrals to an HFM application from a text file")]
        public void LoadJournals(
                [Parameter("Path to the source journal file(s). To load multiple files from the same " +
                           "source directory, use wildcards in the file name",
                           Alias = "JournalFile")]
                string journalFiles,
                [Parameter("Path to the folder in which to create log files; if not specified, defaults to same folder " +
                           "as the source journal file. Log files have the same file name (but with a .log extension) as " +
                           "the file from which the journals are loaded",
                           DefaultValue = null)]
                string logDir,
                LoadOptions options,
                SystemInfo si,
                IOutput output)
        {
            string logFile;

            var paths = FileUtilities.GetMatchingFiles(journalFiles);
            _log.InfoFormat("Found {0} journal files to process", paths.Count);
            output.InitProgress("Journal Load", paths.Count);
            foreach(var jnlFile in paths) {
                if(logDir == null || logDir == "") {
                    logFile = Path.ChangeExtension(jnlFile, ".log");
                }
                else {
                    logFile = Path.Combine(logDir, Path.ChangeExtension(
                                    Path.GetFileName(jnlFile), ".log"));
                }

                // Ensure data file exists and logFile is writeable
                FileUtilities.EnsureFileExists(jnlFile);
                FileUtilities.EnsureFileWriteable(logFile);

                _log.InfoFormat("Loading journals from {0}", jnlFile);
                HFM.Try("Loading data", () => {
                    si.MonitorBlockingTask(output);
                    HsvJournalLoadACV.Load(jnlFile, logFile);
                    si.BlockingTaskComplete();
                });
            }
            output.EndProgress();
        }


        [Command("Extracts journals from an HFM application to a text file")]
        public void ExtractJournals(
                [Parameter("Path to the generated journal extract file")]
                string journalFile,
                [Parameter("Path to the extract log file; if not specified, defaults to same path " +
                           "and name as journal file.", DefaultValue = null)]
                string logFile,
                [Parameter("The scenario to include in the extract")]
                string scenario,
                [Parameter("The year to include in the extract")]
                string year,
                [Parameter("The period to include in the extract")]
                string period,
                ExtractOptions options,
                Metadata metadata)
        {
            options["Scenario"] = metadata["Scenario"].GetId(scenario);
            options["Year"] = metadata["Year"].GetId(year);
            options["Period"] = metadata["Period"].GetId(period);

            if(logFile == null || logFile == "") {
                logFile = Path.ChangeExtension(journalFile, ".log");
            }

            // Ensure dataFile and logFile are writeable locations
            FileUtilities.EnsureFileWriteable(journalFile);
            FileUtilities.EnsureFileWriteable(logFile);

            HFM.Try("Extracting journals",
                    () => HsvJournalLoadACV.Extract(journalFile, logFile));
        }

    }

}

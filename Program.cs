using CommandLine;

using iText.IO.Image;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Xobject;
using iText.Layout;
using iText.Layout.Element;
using iText.Kernel.Geom;

/*
 Future Considerations:
    Add a page to use instead of having a blank page between them.

    Add an option to have the directory name be the name of the ouptput pdf, this would also have to keep the files separate by directory...

    Add pages to the beginning or end of the document
        - finish adding all of the images, and save that document
        - append the end pages
        - prepend the title or other attributions, call to action pages, etc.

    Add text to the top or bottom of each page
        - would be nice to accompany the images, this could also be used for short stories although the layout would be bland
**/

namespace TileableImagesToPdf
{
    class Options
    {
        [Option('d', "directories", Required = false, HelpText = "Input directories to be processed.")]
        public IEnumerable<string>? InputDirectories { get; set; }

        [Option('v', "verbose", Required = false, HelpText = "Prints all messages to standard output.")]
        public bool? Verbose { get; set; }

        [Option('r', "recursive", Required = false, HelpText = "Recursively search the specified directories and sub-directories for all images to combine all images in sub-directories into one pdf")]
        public bool? Recursive { get; set; }

        [Option('o', "output", Required = false, HelpText = "Specify a directory (Example: \"C:\\Projects\\\") to output the pdf files in, the directory where the images were found will be used if none is specified.")]
        public string? OutputDirectory { get; set; }

        [Option('n', "name", Required = false, HelpText = "Specify a file name for the output pdf. Defaults to \"output\" if none is specified")]
        public string? PDFFileName { get; set; }

        [Option('t', "tileable", Required = false, HelpText = "Tiles images on the pages instead of filling one page per image.")]
        public bool? Tileable { get; set; }

        [Option('s', "skip", Required = false, HelpText = "Skips a page between images, useful for creating a coloring book or similar style activity book which requires writing or drawing with markers which may bleed through pages.")]
        public bool? SkipPages { get; set; }

        [Option("randomize", Required = false, HelpText = "Randomizes the order of the images found")]
        public bool? RandomizeImages { get; set; }
        
        [Option('p', "preface", Required = false, HelpText = "Adds the specified pdf to the beginning of the book.")]
        public string? Preface { get; set; }

        [Option('b', "backmatter", Required = false, HelpText = "Adds the specified pdf to the end of the book.")]
        public string? BackMatter { get; set; }

    }

    class Program
    {
        static void LogMessage(string message, Options opts)
        {
            if (opts.Verbose != null && opts.Verbose.Value)
                Console.WriteLine(message);
        }

        public static string[] GetFilesFrom(string searchFolder, string[] filters, bool isRecursive)
        {
            List<string> filesFound = new List<string>();
            var searchOption = isRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (var filter in filters)
            {
                filesFound.AddRange(Directory.GetFiles(searchFolder, string.Format("*.{0}", filter), searchOption));
            }
            return filesFound.ToArray();
        }

        static void RunOptions(Options opts)
        {
            LogMessage("Starting!", opts);

            if (opts.InputDirectories != null)
            {
                foreach (string directory in opts.InputDirectories)
                    GeneratePDF(directory, opts);
                return;
            }

            GeneratePDF(null, opts);

            LogMessage("Done!", opts);
        }

        static void HandleParseError(IEnumerable<Error> errs)
        {
            if (!errs.Any())
                return;

            foreach (Error error in errs)
            {
                // TODO: probably should have proper error handling and logging but yolo
                Console.WriteLine(error); 
            }
        }

        static void Main(string[] args)
        {
            foreach (var arg in args)
                Console.WriteLine(arg);
            Parser.Default.ParseArguments<Options>(args)
             .WithParsed(RunOptions)
             .WithNotParsed(HandleParseError);
        }

        // A method that takes an array of strings and shuffles it randomly using the Fisher-Yates algorithm
        static void ShuffleArray(string[] array)
        {
            // Create a random number generator
            Random random = new Random();

            // Loop through the array from the last element to the first
            for (int i = array.Length - 1; i > 0; i--)
            {
                // Pick a random index between 0 and i
                int j = random.Next(i + 1);

                // Swap the elements at i and j
                string temp = array[i];
                array[i] = array[j];
                array[j] = temp;
            }
        }

        static void GeneratePDF(string? directory, Options opts)
        {
            // Specify the pdf template file
            string templateFile = "C:\\Projects\\ImageTest\\template.pdf";
            
            // Specify the output file name
            string outputFile = $"C:\\Projects\\ImageTest\\{opts.PDFFileName ?? "output"}.pdf";

            FileInfo outFile = new FileInfo(outputFile);

            // Add directory for the outputFile if it does not exist
            if (outFile.Directory != null && !outFile.Directory.Exists)
                outFile.Directory.Create();

            // Verify that the template file exists
            FileInfo srcFile = new FileInfo(templateFile);
            if (!srcFile.Exists)
            {
                LogMessage($"Exiting: Template file was not found here: {templateFile}", opts);
            }

            // Create a copy of the template file
            srcFile.CopyTo(outputFile, true);

            PdfDocument resultDoc = new PdfDocument(new PdfWriter(outputFile));
            PdfDocument srcDoc = new PdfDocument(new PdfReader(templateFile));

            var srcPageSizeRect = srcDoc.GetLastPage().GetPageSize();
            PageSize pageSize = new PageSize(srcPageSizeRect);

            Document doc = new Document(resultDoc, pageSize);

            doc.SetMargins(0, 0, 0, 0);

            string curDir = Directory.GetCurrentDirectory();
            if (directory != null)
            {
                curDir = directory;
            }

            var formatFilter = new string[] { "jpg", "jpeg", "png", "gif", "tiff", "bmp", "svg" };
            var imageFiles = GetFilesFrom(curDir, formatFilter, opts.Recursive ?? false);

            LogMessage($"found {imageFiles.Length} images to add!", opts);

            string pdfLocation = $"template.pdf";
            LogMessage($"loading pdf from {pdfLocation}", opts);
            
            if (opts.RandomizeImages != null && opts.RandomizeImages.Value)
            {
                ShuffleArray(imageFiles);
            }
            
            int offset = 0;
            if (opts.Preface != null)
            {
                // Load the preface
                PdfDocument prefaceDoc = new PdfDocument(new PdfReader(opts.Preface));
                offset = prefaceDoc.GetNumberOfPages();

                // Copy each page to the new document
                prefaceDoc.CopyPagesTo(1, offset, resultDoc);
            }

            // Loop through each image file
            for (int curImageCount = 0; curImageCount < imageFiles.Length; curImageCount++)
            {
                // If this is the last page then do not make a new page
                if (curImageCount < imageFiles.Length)
                {
                    LogMessage($"Adding a new page to the document, currently it has {resultDoc.GetNumberOfPages()} pages!", opts);
                    resultDoc.AddNewPage(pageSize);
                    LogMessage($"Page was added, now it has {resultDoc.GetNumberOfPages()} pages!", opts);
                }

                // Create an image object from the image file
                Image image = new Image(ImageDataFactory.Create(imageFiles[curImageCount]));
                                                
                // Get the width and height of the image
                float imageWidth = image.GetImageWidth();
                float imageHeight = image.GetImageHeight();

                // Get the width and height of the page
                float pageWidth = pageSize.GetWidth();
                float pageHeight = pageSize.GetHeight();

                int pageToAddImageTo = offset + curImageCount + 1 + (opts.SkipPages.HasValue ? curImageCount : 0);

                // Check if the image is tileable
                if (opts.Tileable != null && opts.Tileable.Value)
                {
                    LogMessage($"Starting to tile images!", opts);

                    // Calculate how many times to repeat the image horizontally and vertically
                    int horizontalRepeats = (int)Math.Ceiling(pageWidth / imageWidth);
                    int verticalRepeats = (int)Math.Ceiling(pageHeight / imageHeight);

                    // Loop through each repeat position
                    for (int i = 0; i < horizontalRepeats; i++)
                    {
                        for (int j = 0; j < verticalRepeats; j++)
                        {
                            // Create a copy of the image object
                            Image copy = new Image((PdfImageXObject)image.GetXObject());

                            // Set its position on the page according to its index
                            copy.SetFixedPosition(pageToAddImageTo, i * imageWidth, j * imageHeight);

                            // Add it to the document
                            doc.Add(copy);
                        }
                    }
                }
                else
                {
                    LogMessage($"Starting to add a single image!", opts);

                    // TODO: might have to scale the image if the aspect ratios are super off, if so check here:
                    //      https://kb.itextpdf.com/home/it7kb/examples/large-image-examples

                    // These preserve the aspect ratio
                    //image.SetAutoScale(true);
                    //image.ScaleToFit(pageWidth, pageHeight);

                    // Set the image to fit the page size regardless of aspect ratio so there is no white space
                    // For some reason there was space at the top of the page without adding height even though the margins were 0, hence the magic 8 offset. 7 was too little.
                    image.ScaleAbsolute(pageWidth, pageHeight + 8);

                    // Add the original image to the document
                    image.SetFixedPosition(pageToAddImageTo, 0, 0);
                    doc.Add(image);
                }

                if (opts.SkipPages != null && opts.SkipPages.Value && curImageCount < imageFiles.Length)
                {
                    // Add an empty page to the document before adding the image page
                    //doc.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
                    resultDoc.AddNewPage(pageSize);
                }
            }

            if (opts.BackMatter != null)
            {
                // Load the BackMatter
                PdfDocument backMatterDoc = new PdfDocument(new PdfReader(opts.BackMatter));
                
                // Copy each page to the new document
                backMatterDoc.CopyPagesTo(1, backMatterDoc.GetNumberOfPages(), resultDoc);
            }

            // Close the document and the pdf objects
            doc.Close();
            LogMessage("Finished generating a pdf!", opts);
        }
    }
}

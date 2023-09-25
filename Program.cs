using CommandLine;

using iText.IO.Image;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Xobject;
using iText.Layout;
using iText.Layout.Element;
using iText.Kernel.Geom;

/*  Future Considerations:

    Add a separator page to use instead of having a blank page between them.

    Add an option to have the directory name be the name of the ouptput pdf, this would also have to keep the files separate by directory...

    Add text to the top or bottom of each page
        - would be nice to accompany the images, this could also be used for short stories although the layout would be bland
        - probably better to just do this by hand, it would likely look to robotic for stories 
**/

/* resources 
    Amazon KDP Manuscript guidelines https://kdp.amazon.com/en_US/help/topic/G202145060
    Amazon KDP Sizing Info - https://kdp.amazon.com/en_US/help/topic/GVBQ3CMEQW3W2VL6
 **/

namespace TileableImagesToPdf
{
    class Options
    {
        [Option('b', "backmatter", Required = false, HelpText = "Adds the specified pdf to the end of the book.")]
        public string? BackMatter { get; set; }

        [Option('d', "directories", Required = false, HelpText = "Input directories to be processed.")]
        public IEnumerable<string>? InputDirectories { get; set; }

        [Option('n', "name", Required = false, HelpText = "Specify a file name for the output pdf. Defaults to \"output\" if none is specified")]
        public string? PDFFileName { get; set; }

        [Option('o', "output", Required = false, HelpText = "Specify a directory (Example: \"C:\\Projects\\\") to output the pdf files in, the directory where the images were found will be used if none is specified.")]
        public string? OutputDirectory { get; set; }

        [Option("frontmatter", Required = false, HelpText = "Adds the specified pdf to the beginning of the book.")]
        public string? FrontMatter { get; set; }

        [Option("randomize", Required = false, HelpText = "Randomizes the order of the images found")]
        public bool? RandomizeImages { get; set; }

        [Option('r', "recursive", Required = false, HelpText = "Recursively search the specified directories and sub-directories for all images to combine all images in sub-directories into one pdf")]
        public bool? Recursive { get; set; }

        [Option('s', "skip", Required = false, HelpText = "Skips a page between images, useful for creating a coloring book or similar style activity book which requires writing or drawing with markers which may bleed through pages.")]
        public bool? SkipPages { get; set; }

        [Option("template", Required = false, HelpText = "Template to use to combine everything into.")]
        public string? Template { get; set; }

        [Option('t', "tileable", Required = false, HelpText = "Tiles images on the pages instead of filling one page per image.")]
        public bool? Tileable { get; set; }

        [Option('v', "verbose", Required = false, HelpText = "Prints all messages to standard output.")]
        public bool? Verbose { get; set; }
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

            if (opts.InputDirectories != null && opts.InputDirectories.Count() > 0)
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
            // Specify the output file name
            string outputFile = $"{opts.OutputDirectory ?? "C:\\Projects\\ImageTest\\"}{opts.PDFFileName ?? "output"}.pdf";

            FileInfo outFile = new FileInfo(outputFile);

            // Add directory for the outputFile if it does not exist
            if (outFile.Directory != null && !outFile.Directory.Exists)
                outFile.Directory.Create();

            // Specify the pdf template file
            string templateFile = "template.pdf";
            if (opts.Template != null)
                templateFile = opts.Template;

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
            var templatePage = srcDoc.GetFirstPage();
            Document doc = new Document(resultDoc, pageSize);

            // 0.375 inches required for inside gutter and outside margins if using bleed -- https://kdp.amazon.com/en_US/help/topic/GVBQ3CMEQW3W2VL6
            // 1 inch is 72 points
            // Setting the margins on the document appears to do nothing since the image is set to absolute
            // We account for this by adding the margin to the image size and position as well, see image.ScaleAbsolute
            float marginSize = 9f;
            doc.SetMargins(marginSize, marginSize, marginSize, marginSize);

            string curDir = Directory.GetCurrentDirectory();
            if (directory != null)
            {
                curDir = directory;
            }

            // Retrieve images from specified directory that are acceptable formats to load
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
            if (opts.FrontMatter != null)
            {
                // Load the front matter
                PdfDocument frontMatterDoc = new PdfDocument(new PdfReader(opts.FrontMatter));
                offset = frontMatterDoc.GetNumberOfPages();

                // Copy each page to the new document
                frontMatterDoc.CopyPagesTo(1, offset, resultDoc);
                frontMatterDoc.Close();
            }

            // Loop through each image file
            for (int curImageCount = 0; curImageCount < imageFiles.Length; curImageCount++)
            {
                int pageToAddImageTo = offset + curImageCount + 1 + (opts.SkipPages.HasValue ? curImageCount : 0);

                // If this is the last page then do not make a new page
                if (curImageCount < imageFiles.Length)
                {
                    LogMessage($"Adding a new page to the document, currently it has {resultDoc.GetNumberOfPages()} pages!", opts);

                    // Copy the template page to the new document
                    PdfDocument templateDoc = new PdfDocument(new PdfReader(templateFile));
                    templateDoc.CopyPagesTo(1, 1, resultDoc);
                    templateDoc.Close();

                    LogMessage($"Page was added, now it has {resultDoc.GetNumberOfPages()} pages!", opts);
                }

                // Create an image object from the image file
                Image image = new Image(ImageDataFactory.Create(imageFiles[curImageCount]));
                                                
                // Get the width and height of the image
                // Image size was seemingly in pixels
                float imageWidth = image.GetImageWidth();
                float imageHeight = image.GetImageHeight();

                // Get the width and height of the page
                // Width and height of the page are in points - 72 points per inch?
                float pageWidth = pageSize.GetWidth(); //template width is read as 621
                float pageHeight = pageSize.GetHeight(); //template height is read as 810

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

                    // These lines would alternatively preserve the aspect ratio if that is ever desired in the future...
                    //image.SetAutoScale(true);
                    //image.ScaleToFit(pageWidth, pageHeight);

                    // Set the image to fit the page size regardless of aspect ratio so there is no white space
                    // Note: Previously before adding marginSize, for some reason there was space at the top of the page without adding height even though the margins were 0, hence the magic 8 offset. 7 was too little.
                    image.ScaleAbsolute(pageWidth - (marginSize * 2), pageHeight - (marginSize * 2));

                    // Add the original image to the document
                    image.SetFixedPosition(pageToAddImageTo, marginSize, marginSize);
                    doc.Add(image);
                }

                if (opts.SkipPages != null && opts.SkipPages.Value && curImageCount < imageFiles.Length)
                {
                    // Copy the template page to the new document
                    PdfDocument templateDoc = new PdfDocument(new PdfReader(templateFile));
                    templateDoc.CopyPagesTo(1, 1, resultDoc);
                    templateDoc.Close();
                }
            }

            if (opts.BackMatter != null)
            {
                // Load the BackMatter
                PdfDocument backMatterDoc = new PdfDocument(new PdfReader(opts.BackMatter));
                
                // Copy each page to the new document
                backMatterDoc.CopyPagesTo(1, backMatterDoc.GetNumberOfPages(), resultDoc);
                backMatterDoc.Close();
            }

            // Close the document and the pdf objects
            doc.Close();
            LogMessage("Finished generating a pdf!", opts);
        }
    }
}


/* Debug Params


Full Working Examples
--directories "G:\\My Drive\\Books\\Coloring Books\\Stained Glass\\Vol1\\" --output "G:\\My Drive\\Books\\Coloring Books\\Stained Glass\\Vol1\\" --name "vol1234" --verbose true --recursive true --tileable false --skip true --randomize true --frontmatter "G:\\My Drive\\Books\\Template Pages\\frontmatter.pdf" --template "G:\\My Drive\\Books\\Template Pages\\template.pdf"
--directories "G:\\My Drive\\Books\\Coloring Books\\Stained Glass\\Vol1\\" --output "G:\\My Drive\\Books\\Coloring Books\\Stained Glass\\Vol1\\" --name "vol1234" --verbose true --recursive true --tileable false --skip true --randomize true --frontmatter "G:\\My Drive\\Books\\Template Pages\\frontmatter.pdf" --template "G:\\My Drive\\Books\\Template Pages\\template.pdf"

--directories "G:\\My Drive\\Books\\Coloring Books\\Stained Glass\\Vol1\\Images\\" --output "G:\\My Drive\\Books\\Coloring Books\\Stained Glass\\Vol1\\" --name "vol1_nobleed" --verbose true --recursive true --tileable false --skip true --randomize true --frontmatter "G:\\My Drive\\Books\\Template Pages\\frontmatter_nobleed.pdf" --template "G:\\My Drive\\Books\\Template Pages\\template_nobleed.pdf"
--directories "G:\\My Drive\\Books\\Coloring Books\\Patterns\\Vol1\\Images\\" --output "G:\\My Drive\\Books\\Coloring Books\\Patterns\\Vol1\\" --name "vol1_nobleed" --verbose true --recursive true --tileable false --skip true --randomize true --frontmatter "G:\\My Drive\\Books\\Template Pages\\frontmatter_nobleed.pdf" --template "G:\\My Drive\\Books\\Template Pages\\template_nobleed.pdf"
--directories "G:\\My Drive\\Books\\Coloring Books\\Patterns\\Vol2\\Images\\" --output "G:\\My Drive\\Books\\Coloring Books\\Patterns\\Vol2\\" --name "vol2_nobleed" --verbose true --recursive true --tileable false --skip true --randomize true --frontmatter "G:\\My Drive\\Books\\Template Pages\\frontmatter_nobleed.pdf" --template "G:\\My Drive\\Books\\Template Pages\\template_nobleed.pdf"

Originally the uploads to amazon were being rejected, seemingly because there were white pixels around the edges of the pages or maybe because the images did not have a margin big enough around the images, no one will ever know... the page size was the same and was using the recommended size in the email...
--directories "G:\\My Drive\\Books\\Coloring Books\\Stained Glass\\Vol1\\Images\\" --output "G:\\My Drive\\Books\\Coloring Books\\Stained Glass\\Vol1\\" --name "vol1_black" --verbose true --recursive true --tileable false --skip true --randomize true --frontmatter "G:\\My Drive\\Books\\Template Pages\\frontmatter.pdf" --template "G:\\My Drive\\Books\\Template Pages\\template.pdf"
--directories "G:\\My Drive\\Books\\Coloring Books\\Patterns\\Vol1\\Images\\" --output "G:\\My Drive\\Books\\Coloring Books\\Patterns\\Vol1\\" --name "vol1_black" --verbose true --recursive true --tileable false --skip true --randomize true --frontmatter "G:\\My Drive\\Books\\Template Pages\\frontmatter.pdf" --template "G:\\My Drive\\Books\\Template Pages\\template.pdf"
--directories "G:\\My Drive\\Books\\Coloring Books\\Patterns\\Vol2\\Images\\" --output "G:\\My Drive\\Books\\Coloring Books\\Patterns\\Vol2\\" --name "vol2_black" --verbose true --recursive true --tileable false --skip true --randomize true --frontmatter "G:\\My Drive\\Books\\Template Pages\\frontmatter.pdf" --template "G:\\My Drive\\Books\\Template Pages\\template.pdf"

--directories "G:\\My Drive\\Books\\Coloring Books\\Stained Glass\\Vol1\\Images\\" --output "G:\\My Drive\\Books\\Coloring Books\\Stained Glass\\Vol1\\" --name "vol1_white" --verbose true --recursive true --tileable false --skip true --randomize true --frontmatter "G:\\My Drive\\Books\\Template Pages\\frontmatter.pdf" --template "G:\\My Drive\\Books\\Template Pages\\template_white.pdf"
--directories "G:\\My Drive\\Books\\Coloring Books\\Patterns\\Vol1\\Images\\" --output "G:\\My Drive\\Books\\Coloring Books\\Patterns\\Vol1\\" --name "vol1_white" --verbose true --recursive true --tileable false --skip true --randomize true --frontmatter "G:\\My Drive\\Books\\Template Pages\\frontmatter.pdf" --template "G:\\My Drive\\Books\\Template Pages\\template_white.pdf"
--directories "G:\\My Drive\\Books\\Coloring Books\\Patterns\\Vol2\\Images\\" --output "G:\\My Drive\\Books\\Coloring Books\\Patterns\\Vol2\\" --name "vol2_white" --verbose true --recursive true --tileable false --skip true --randomize true --frontmatter "G:\\My Drive\\Books\\Template Pages\\frontmatter.pdf" --template "G:\\My Drive\\Books\\Template Pages\\template_white.pdf"


--directories "G:\\My Drive\\Books\\Coloring Books\\Stained Glass\\Vol1\\Images\\" --output "G:\\My Drive\\Books\\Coloring Books\\Stained Glass\\Vol1\\" --name "vol1_nobleed" --verbose true --recursive true --tileable false --skip true --randomize true --frontmatter "G:\\My Drive\\Books\\Template Pages\\frontmatter_nobleed.pdf" --template "G:\\My Drive\\Books\\Template Pages\\template_nobleed.pdf"
--directories "G:\\My Drive\\Books\\Coloring Books\\Patterns\\Vol1\\Images\\" --output "G:\\My Drive\\Books\\Coloring Books\\Patterns\\Vol1\\" --name "vol1_nobleed" --verbose true --recursive true --tileable false --skip true --randomize true --frontmatter "G:\\My Drive\\Books\\Template Pages\\frontmatter_nobleed.pdf" --template "G:\\My Drive\\Books\\Template Pages\\template_nobleed.pdf"
--directories "G:\\My Drive\\Books\\Coloring Books\\Patterns\\Vol2\\Images\\" --output "G:\\My Drive\\Books\\Coloring Books\\Patterns\\Vol2\\" --name "vol2_nobleed" --verbose true --recursive true --tileable false --skip true --randomize true --frontmatter "G:\\My Drive\\Books\\Template Pages\\frontmatter_nobleed.pdf" --template "G:\\My Drive\\Books\\Template Pages\\template_nobleed.pdf"


Making new frontmatter -- combining 2 pdf files with an empty directory of images
--directories "C:\\Projects\\ImageTest\\Two" --output "G:\\My Drive\\Books\\Template Pages\\" --name "frontmatter" --verbose true --recursive true --tileable false --skip false --randomize false --frontmatter "G:\\My Drive\\Books\\Template Pages\\copyright.pdf" --template "G:\\My Drive\\Books\\Template Pages\\template.pdf" --backmatter "G:\\My Drive\\Books\\Template Pages\\belongsto.pdf"
--directories "C:\\Projects\\ImageTest\\Two" --output "G:\\My Drive\\Books\\Template Pages\\" --name "frontmatter_nobleed" --verbose true --recursive true --tileable false --skip false --randomize false --frontmatter "G:\\My Drive\\Books\\Template Pages\\copyright_nobleed.pdf" --template "G:\\My Drive\\Books\\Template Pages\\template_nobleed.pdf" --backmatter "G:\\My Drive\\Books\\Template Pages\\belongsto_nobleed.pdf"
 **/

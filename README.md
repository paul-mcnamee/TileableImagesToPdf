# TileableImagesToPdf
Creates a PDF from tileable or normal images, filling each page with the images, and optionally skipping a page between images.

## Command line args

  -b, --backmatter     Adds the specified pdf to the end of the book.

  -d, --directories    Input directories to be processed.

  -n, --name           Specify a file name for the output pdf. Defaults to "output" if none is specified

  -o, --output         Specify a directory (Example: "C:\Projects\") to output the pdf files in, the directory where the images were found will be used if none is specified.

  -p, --preface        Adds the specified pdf to the beginning of the book.

  --randomize          Randomizes the order of the images found

  -r, --recursive      Recursively search the specified directories and sub-directories for all images to combine all images in sub-directories into one pdf

  -s, --skip           Skips a page between images, useful for creating a coloring book or similar style activity book which requires writing or drawing with markers which may bleed through pages.

  --template           Template to use to combine everything into.

  -t, --tileable       Tiles images on the pages instead of filling one page per image.

  -v, --verbose        Prints all messages to standard output.

  --help               Display this help screen.

  --version            Display version information.

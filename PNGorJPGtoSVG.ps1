# NOTE: this kinda sucks, but feel free to check resulting svg since it does work, just not amazing results. Might be good for some thicker monochrome line art stuff


# Get the input directory name from the user
$dir = Read-Host -Prompt "Enter the directory name"

# Get all the png or jpg files in the input directory and its subdirectories
$files = Get-ChildItem -Path $dir -Include *.png, *.jpg -Recurse

# Get the output directory name from the user
$out = Read-Host -Prompt "Enter the output directory name"

# Create the output directory if it does not exist
if (-not (Test-Path $out)) {
    New-Item -ItemType Directory -Path $out
}

# Loop through each file and convert it to an svg using ImageMagick
foreach ($file in $files) {
    # Get the file name without extension
    $name = $file.BaseName

    # Set the output file name with svg extension
    $output = Join-Path $out "$name.svg"

    # Convert the file to svg using ImageMagick
    magick convert $file $output
}

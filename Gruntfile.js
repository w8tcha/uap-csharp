/**
 * Build process for YetAnotherForum.NET
 *
 * Don't know where to start?
 * Try: http://24ways.org/2013/grunt-is-not-weird-and-hard/
 */
module.exports = function(grunt) {


    // CONFIGURATION
    grunt.initConfig({
        pkg: grunt.file.readJSON("package.json"),
        
        devUpdate: {
            main: {
                options: {
                    reportUpdated: true,
                    updateType: "force",
                    semver: false,
                    packages: {
                        devDependencies: true, //only check for devDependencies
                        dependencies: true
                    }
                }
            }
        }
    });

    // PLUGINS
    grunt.loadNpmTasks("@w8tcha/grunt-dev-update");

    grunt.registerTask("default",
        [
            "devUpdate"
        ]);
};